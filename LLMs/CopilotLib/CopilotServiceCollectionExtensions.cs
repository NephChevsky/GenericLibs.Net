using LLMLib;
using Microsoft.Extensions.DependencyInjection;

namespace CopilotLib
{
	public static class CopilotServiceCollectionExtensions
	{
		public static IServiceCollection AddCopilotLib(this IServiceCollection services, Action<CopilotOptions>? configure = null)
		{
			CopilotOptions options = new();
			configure?.Invoke(options);

			services.AddSingleton(options);
			return services.AddSingleton<ILLMService, CopilotService>();
		}
	}
}
