using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotifierLib;

namespace DiscordLib
{
	public static class DiscordServiceCollectionExtensions
	{
		public static IServiceCollection AddDiscordLib(this IServiceCollection services)
		{
			services.AddHttpClient();
			services.TryAddSingleton<Discord>();
			return services;
		}

		public static IServiceCollection AddDiscordNotifierLib(this IServiceCollection services)
		{
			services.AddDiscordLib();
			return services.AddSingleton<INotifier>(sp => sp.GetRequiredService<Discord>());
		}
	}
}
