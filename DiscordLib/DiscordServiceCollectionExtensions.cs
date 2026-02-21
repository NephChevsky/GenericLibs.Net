using Microsoft.Extensions.DependencyInjection;
using NotifierLib;

namespace DiscordLib
{
	public static class DiscordServiceCollectionExtensions
	{
		public static IServiceCollection AddDiscord(this IServiceCollection services)
		{
			return services.AddSingleton<Discord>(); ;
		}

		public static IServiceCollection AddDiscordNotifier(this IServiceCollection services)
		{
			return services.AddSingleton<INotifier, Discord>();
		}
	}
}
