using JwtLib.DTO;
using JwtLib.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
	public abstract class AuthController<TDbContext, TUser, TDevice>(ILogger logger, TDbContext db, IConfiguration configuration, INotifier notifier, IHostEnvironment environment, DbSet<TUser> users, DbSet<TDevice> devices) : ControllerBase
		where TDbContext : DbContext
		where TUser : class, IJwtUser, new()
		where TDevice : class, IJwtDevice, new()
	{
		protected readonly ILogger _logger = logger;
		protected readonly TDbContext _db = db;
		protected readonly IConfiguration _configuration = configuration;
		protected readonly INotifier _notifier = notifier;
		protected readonly IHostEnvironment _environment = environment;

		private static readonly List<DateTime> _loggingTries = [];

		protected readonly DbSet<TUser> Users = users;
		protected readonly DbSet<TDevice> Devices = devices;

		protected virtual string GuestNamePrefix => "guest-";

		protected virtual string GuestRoleName => "Guest";

		protected virtual string DefaultRoleName => "User";

		protected virtual bool GuestAccountsEnabled => _configuration.GetValue("JwtSettings:AllowGuestAccountCreation", false);

		protected virtual bool UsernameChangeEnabled => _configuration.GetValue("JwtSettings:AllowUsernameChange", false);

		protected virtual bool RegistrationEnabled => _configuration.GetValue("JwtSettings:AllowRegistration", false);

		protected virtual bool CookieSecure => !_environment.IsDevelopment() || _configuration.GetValue("JwtSettings:CookieSecure", true);

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

		private CookieOptions GetCookieOptions()
		{
			return new CookieOptions
			{
				HttpOnly = true,
				Secure = CookieSecure,
				SameSite = SameSiteMode.Strict,
				Expires = DateTime.UtcNow.AddDays(7),
				Path = "/"
			};
		}

		private async Task<AuthLoginResponse> IssueSessionAsync(TUser user)
		{
			(string accessToken, string refreshToken) = GenerateTokens(user.Id, user.Role);

			TDevice? device = Devices.FirstOrDefault(d => d.OwnerId == user.Id && d.Name == Request.Headers.UserAgent.ToString());
			if (device == null)
			{
				device = new TDevice
				{
					Id = Guid.NewGuid(),
					OwnerId = user.Id,
					Name = Request.Headers.UserAgent.ToString()
				};
				await Devices.AddAsync(device);
			}

			device.RefreshToken = ComputeSha256(refreshToken);
			device.RefreshTokenExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromDays(7));

			await _db.SaveChangesAsync();

			Response.Cookies.Append("refresh_token", refreshToken, GetCookieOptions());

			return new AuthLoginResponse(accessToken);
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
						AuthLoginResponse session = await IssueSessionAsync(dbUser);

						_logger.LogInformation("{User} logged successfully", request.Username);
						return Ok(session);
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

		[AllowAnonymous]
		[HttpPost("guest")]
		public async Task<IActionResult> CreateGuest()
		{
			if (!GuestAccountsEnabled)
			{
				return NotFound();
			}

			try
			{
				string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

				TUser user = new()
				{
					Id = Guid.NewGuid(),
					Name = $"{GuestNamePrefix}{Guid.NewGuid():N}",
					PasswordHash = BCrypt.Net.BCrypt.HashPassword(secret),
					Role = GuestRoleName
				};

				await Users.AddAsync(user);
				await _db.SaveChangesAsync();

				string recoveryCode = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.Id}:{secret}"));

				AuthLoginResponse session = await IssueSessionAsync(user);

				_logger.LogInformation("Guest account {UserId} created", user.Id);

				return Ok(new AuthGuestResponse(session.Token, recoveryCode));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while creating a guest account");
				return StatusCode(503, "Failed to reach database");
			}
		}

		[AllowAnonymous]
		[HttpPost("guest/redeem")]
		public async Task<IActionResult> RedeemGuest([FromBody] AuthRedeemGuestRequest request)
		{
			string decoded;
			try
			{
				decoded = Encoding.UTF8.GetString(Convert.FromBase64String(request.Code));
			}
			catch (FormatException)
			{
				return Unauthorized("Invalid recovery code.");
			}

			int separatorIndex = decoded.IndexOf(':');
			if (separatorIndex <= 0 || !Guid.TryParse(decoded[..separatorIndex], out Guid userId))
			{
				return Unauthorized("Invalid recovery code.");
			}

			string secret = decoded[(separatorIndex + 1)..];

			try
			{
				TUser? dbUser = Users.FirstOrDefault(u => u.Id == userId);
				if (dbUser == null || !BCrypt.Net.BCrypt.Verify(secret, dbUser.PasswordHash))
				{
					_logger.LogWarning("Failed guest recovery attempt for user {UserId}", userId);
					return Unauthorized("Invalid recovery code.");
				}

				AuthLoginResponse session = await IssueSessionAsync(dbUser);

				_logger.LogInformation("Guest account {UserId} logged back in via recovery code", userId);

				return Ok(session);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while redeeming a guest recovery code for user {UserId}", userId);
				return StatusCode(503, "Failed to reach database");
			}
		}

		[AllowAnonymous]
		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] AuthSetCredentialsRequest request)
		{
			if (!RegistrationEnabled)
			{
				return NotFound();
			}

			if (string.IsNullOrWhiteSpace(request.Username) || request.Username.StartsWith(GuestNamePrefix, StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest("Invalid username.");
			}

			if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
			{
				return BadRequest("Password must be at least 8 characters long.");
			}

			try
			{
				bool usernameTaken = Users.Any(u => u.Name == request.Username);
				if (usernameTaken)
				{
					return Conflict("Username is already taken.");
				}

				TUser user = new()
				{
					Id = Guid.NewGuid(),
					Name = request.Username,
					PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
					Role = DefaultRoleName
				};

				await Users.AddAsync(user);
				await _db.SaveChangesAsync();

				AuthLoginResponse session = await IssueSessionAsync(user);

				_logger.LogInformation("Account {UserId} registered with username {Username}", user.Id, request.Username);

				return Ok(session);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while registering an account with username {Username}", request.Username);
				return StatusCode(503, "Failed to reach database");
			}
		}

		[Authorize]
		[HttpPost("claim")]
		public async Task<IActionResult> ClaimAccount([FromBody] AuthSetCredentialsRequest request)
		{
			string? sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			if (sub == null || !Guid.TryParse(sub, out Guid userId))
			{
				return Unauthorized();
			}

			if (string.IsNullOrWhiteSpace(request.Username) || request.Username.StartsWith(GuestNamePrefix, StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest("Invalid username.");
			}

			if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
			{
				return BadRequest("Password must be at least 8 characters long.");
			}

			try
			{
				TUser? dbUser = Users.FirstOrDefault(u => u.Id == userId);
				if (dbUser == null)
				{
					return Unauthorized();
				}

				bool usernameTaken = Users.Any(u => u.Id != userId && u.Name == request.Username);
				if (usernameTaken)
				{
					return Conflict("Username is already taken.");
				}

				dbUser.Name = request.Username;
				dbUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
				if (dbUser.Role == GuestRoleName)
				{
					dbUser.Role = DefaultRoleName;
				}

				await _db.SaveChangesAsync();

				_logger.LogInformation("User {UserId} claimed account with username {Username}", userId, request.Username);

				return Ok();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while claiming an account for user {UserId}", userId);
				return StatusCode(503, "Failed to reach database");
			}
		}

		[Authorize]
		[HttpPost("username")]
		public async Task<IActionResult> ChangeUsername([FromBody] AuthChangeUsernameRequest request)
		{
			if (!UsernameChangeEnabled)
			{
				return NotFound();
			}

			string? sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			if (sub == null || !Guid.TryParse(sub, out Guid userId))
			{
				return Unauthorized();
			}

			if (string.IsNullOrWhiteSpace(request.Username) || request.Username.StartsWith(GuestNamePrefix, StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest("Invalid username.");
			}

			try
			{
				TUser? dbUser = Users.FirstOrDefault(u => u.Id == userId);
				if (dbUser == null)
				{
					return Unauthorized();
				}

				bool usernameTaken = Users.Any(u => u.Id != userId && u.Name == request.Username);
				if (usernameTaken)
				{
					return Conflict("Username is already taken.");
				}

				dbUser.Name = request.Username;
				await _db.SaveChangesAsync();

				_logger.LogInformation("User {UserId} changed their username to {Username}", userId, request.Username);

				return Ok();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while changing username for user {UserId}", userId);
				return StatusCode(503, "Failed to reach database");
			}
		}

		[Authorize]
		[HttpPost("password")]
		public async Task<IActionResult> ChangePassword([FromBody] AuthChangePasswordRequest request)
		{
			string? sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			if (sub == null || !Guid.TryParse(sub, out Guid userId))
			{
				return Unauthorized();
			}

			if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
			{
				return BadRequest("Password must be at least 8 characters long.");
			}

			try
			{
				TUser? dbUser = Users.FirstOrDefault(u => u.Id == userId);
				if (dbUser == null)
				{
					return Unauthorized();
				}

				if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, dbUser.PasswordHash))
				{
					return Unauthorized("Current password is incorrect.");
				}

				dbUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
				await _db.SaveChangesAsync();

				_logger.LogInformation("User {UserId} changed their password", userId);

				return Ok();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occurred while changing password for user {UserId}", userId);
				return StatusCode(503, "Failed to reach database");
			}
		}

		[HttpGet("me")]
		public virtual async Task<IActionResult> GetUser()
		{
			string? sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
			if (sub == null || !Guid.TryParse(sub, out Guid userId))
			{
				return Unauthorized();
			}

			TUser? dbUser = await Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));
			if (dbUser == null)
			{
				return Unauthorized();
			}

			bool isGuest = dbUser.Name.StartsWith(GuestNamePrefix, StringComparison.OrdinalIgnoreCase);
			return Ok(new AuthUserResponse(dbUser.Id, dbUser.Name, dbUser.Role, isGuest));
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
