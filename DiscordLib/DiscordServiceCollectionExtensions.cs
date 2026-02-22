using Microsoft.Extensions.DependencyInjection;
using NotifierLib;

namespace DiscordLib
{
	public static class DiscordServiceCollectionExtensions
	{
		public static IServiceCollection AddDiscordLib(this IServiceCollection services)
		{
			return services.AddSingleton<Discord>(); ;
		}

		public static IServiceCollection AddDiscordNotifierLib(this IServiceCollection services)
		{
			return services.AddSingleton<INotifier, Discord>();
		}
	}
}
