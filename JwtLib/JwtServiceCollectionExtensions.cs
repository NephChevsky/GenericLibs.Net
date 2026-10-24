using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace JwtLib
{
	public static class JwtServiceCollectionExtensions
	{
		public static IServiceCollection AddJwtAuthenticationLib(this IServiceCollection services, IConfiguration configuration)
		{
			IConfigurationSection jwtSettings = configuration.GetSection("JwtSettings");
			string jwtSecretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
			string jwtIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
			string jwtAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = jwtIssuer,
					ValidAudience = jwtAudience,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
					RoleClaimType = "role"
				};
				options.MapInboundClaims = false;
			});

			services.AddAuthorization();

			return services;
		}

		/// <summary>
		/// Lets SignalR clients authenticate with the JWT passed in the <c>access_token</c> query string
		/// (browsers can't set an Authorization header on WebSocket connections) for requests under <paramref name="pathPrefix"/>.
		/// </summary>
		public static IServiceCollection AddJwtSignalRSupport(this IServiceCollection services, string pathPrefix)
		{
			services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
			{
				Func<MessageReceivedContext, Task>? previous = options.Events?.OnMessageReceived;
				options.Events ??= new JwtBearerEvents();
				options.Events.OnMessageReceived = async context =>
				{
					if (previous is not null)
					{
						await previous(context);
					}

					if (string.IsNullOrEmpty(context.Token)
						&& context.Request.Path.StartsWithSegments(pathPrefix)
						&& context.Request.Query.TryGetValue("access_token", out Microsoft.Extensions.Primitives.StringValues token))
					{
						context.Token = token;
					}
				};
			});

			return services;
		}
	}
}
