using Microsoft.Extensions.DependencyInjection;

namespace SshLib
{
	public static class SshServiceCollectionExtensions
	{
		public static IServiceCollection AddSshLib(this IServiceCollection services)
		{
			services.AddSingleton<ISshService, SshService>();
			return services;
		}
	}
}
