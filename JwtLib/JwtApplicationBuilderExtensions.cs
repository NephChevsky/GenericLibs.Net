using Microsoft.AspNetCore.Builder;

namespace JwtLib
{
	public static class JwtApplicationBuilderExtensions
	{
		public static IApplicationBuilder UseJwtAuthenticationLib(this IApplicationBuilder app)
		{
			app.UseAuthentication();
			app.UseAuthorization();

			return app;
		}
	}
}
