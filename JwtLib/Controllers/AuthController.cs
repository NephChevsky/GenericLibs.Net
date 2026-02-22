using JwtLib.DTO;
using JwtLib.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NotifierLib;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace JwtLib.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public abstract class AuthController<TDbContext, TUser, TDevice>(ILogger logger, TDbContext db, IConfiguration configuration, INotifier notifier, DbSet<TUser> users, DbSet<TDevice> devices) : ControllerBase
		where TDbContext : DbContext
		where TUser : class, IJwtUser
		where TDevice : class, IJwtDevice, new()
	{
		protected readonly ILogger _logger = logger;
		protected readonly TDbContext _db = db;
		protected readonly IConfiguration _configuration = configuration;
		protected readonly INotifier _notifier = notifier;

		private static readonly List<DateTime> _loggingTries = [];

		protected readonly DbSet<TUser> Users = users;
		protected readonly DbSet<TDevice> Devices = devices;

		private static string ComputeSha256(string input)
		{
			byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
			return Convert.ToHexString(bytes);
		}

		private (string accessToken, string refreshToken) GenerateTokens(Guid userId, string role)
		{
			Claim[] claims =
			[
				new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
				new Claim("role", role),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			];

			SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!));
			SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

			JwtSecurityToken token = new(
				issuer: _configuration["JwtSettings:Issuer"],
				audience: _configuration["JwtSettings:Audience"],
				claims: claims,
				expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(15)),
				signingCredentials: creds);

			string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
			string refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

			return (accessToken, refreshToken);
		}

		private static CookieOptions GetCookieOptions()
		{
			return new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Strict,
				Expires = DateTime.UtcNow.AddDays(7),
				Path = "/"
			};
		}

		[AllowAnonymous]
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] AuthLoginRequest request)
		{
			_logger.LogInformation("Login endpoint was called");

			_loggingTries.RemoveAll(x => x < DateTime.Now.AddMinutes(-5));

			if (_loggingTries.Count > 5)
			{
				_logger.LogError("Delaying {User} for too many connection attempts", request.Username);
				return StatusCode(429, "Too many requests. Please try again later.");
			}

			try
			{
				TUser? dbUser = Users.FirstOrDefault(u => u.Name == request.Username);
				if (dbUser != null)
				{
					if (BCrypt.Net.BCrypt.Verify(request.Password, dbUser.PasswordHash))
					{
						(string accessToken, string refreshToken) = GenerateTokens(dbUser.Id, dbUser.Role);

						TDevice? device = Devices.FirstOrDefault(d => d.OwnerId == dbUser.Id && d.Name == Request.Headers.UserAgent.ToString());
						if (device == null)
						{
							device = new TDevice
							{
								Id = Guid.NewGuid(),
								OwnerId = dbUser.Id,
								Name = Request.Headers.UserAgent.ToString()
							};
							await Devices.AddAsync(device);
						}

						device.RefreshToken = ComputeSha256(refreshToken);
						device.RefreshTokenExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromDays(7));

						await _db.SaveChangesAsync();

						Response.Cookies.Append("refresh_token", refreshToken, GetCookieOptions());

						_logger.LogInformation("{User} logged successfully", request.Username);
						return Ok(new AuthLoginResponse(accessToken));
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while trying to log in {User}", request.Username);
				return StatusCode(503, "Failed to reach database");
			}

			_loggingTries.Add(DateTime.Now);
			await _notifier.SendNotification($"⚠️ Failed login attempt for user **{request.Username}**");

			_logger.LogError("{User} failed to log in", request.Username);
			return Unauthorized();
		}

		[HttpGet("me")]
		public virtual async Task<IActionResult> GetUser()
		{
			return Ok();
		}

		[AllowAnonymous]
		[HttpPost("refresh")]
		public async Task<IActionResult> Refresh()
		{
			_logger.LogInformation("Refresh endpoint was called from referer {Referer}", Request.Headers.Referer.ToString());

			string? refreshToken = Request.Cookies["refresh_token"];

			if (refreshToken == null)
			{
				_logger.LogWarning("Refresh denied: missing refresh token cookie");
				return Unauthorized("Missing refresh token");
			}

			try
			{
				string providedHash = ComputeSha256(refreshToken);

				TDevice? dbDevice = Devices.FirstOrDefault(d => d.RefreshToken == providedHash);
				if (dbDevice == null)
				{
					_logger.LogWarning("Refresh denied: invalid refresh token");
					return Unauthorized("Invalid refresh token.");
				}

				if (dbDevice.RefreshTokenExpiresAt == null || dbDevice.RefreshTokenExpiresAt < DateTime.UtcNow)
				{
					_logger.LogWarning("Refresh denied: refresh token expired for device {DeviceId}", dbDevice.Id);
					return Unauthorized("Refresh token expired.");
				}

				TUser? dbUser = Users.FirstOrDefault(u => u.Id == dbDevice.OwnerId);
				if (dbUser == null)
				{
					_logger.LogWarning("Refresh denied: device {DeviceId} owner not found", dbDevice.Id);
					return Unauthorized("Invalid device.");
				}

				(string accessToken, refreshToken) = GenerateTokens(dbUser.Id, dbUser.Role);

				if (dbDevice.RefreshTokenExpiresAt != null && dbDevice.RefreshTokenExpiresAt < DateTime.UtcNow.AddDays(6))
				{
					dbDevice.RefreshToken = ComputeSha256(refreshToken);
					dbDevice.RefreshTokenExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromDays(7));

					await _db.SaveChangesAsync();

					Response.Cookies.Append("refresh_token", refreshToken, GetCookieOptions());

					_logger.LogInformation("Issued new refresh token for device {DeviceId} (owner {OwnerId})", dbDevice.Id, dbDevice.OwnerId);
				}

				_logger.LogInformation("Access token refreshed successfully for device {DeviceId} (owner {OwnerId})", dbDevice.Id, dbDevice.OwnerId);

				return Ok(new AuthLoginResponse(accessToken));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while refreshing token");
				return StatusCode(503, "Failed to reach database");
			}
		}

		[AllowAnonymous]
		[HttpPost("logout")]
		public async Task<IActionResult> Logout()
		{
			string? refreshToken = Request.Cookies["refresh_token"];

			if (string.IsNullOrEmpty(refreshToken))
			{
				return Ok();
			}

			string providedHash = ComputeSha256(refreshToken);

			TDevice? dbDevice = Devices.FirstOrDefault(d => d.RefreshToken == providedHash);

			if (dbDevice == null)
			{
				return Ok();
			}

			_db.Remove(dbDevice);
			await _db.SaveChangesAsync();

			return Ok();
		}
	}
}
