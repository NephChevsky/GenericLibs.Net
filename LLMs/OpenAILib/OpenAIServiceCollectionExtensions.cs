using Microsoft.Extensions.DependencyInjection;

namespace OpenAILib
{
    public static class OpenAIServiceCollectionExtensions
    {
        public static IServiceCollection AddOpenAILib(this IServiceCollection services)
        {
            return services.AddSingleton<IOpenAIService, OpenAIService>();
        }
    }
}
