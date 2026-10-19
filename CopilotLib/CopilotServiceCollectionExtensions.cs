using Microsoft.Extensions.DependencyInjection;

namespace CopilotLib
{
	public static class CopilotServiceCollectionExtensions
	{
		public static IServiceCollection AddCopilotLib(this IServiceCollection services)
		{
			return services.AddSingleton<ICopilotService, CopilotService>();
		}
	}
}
