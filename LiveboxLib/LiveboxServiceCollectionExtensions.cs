using Microsoft.Extensions.DependencyInjection;

namespace LiveboxLib;

public static class LiveboxServiceCollectionExtensions
{
	public static IServiceCollection AddLiveboxLib(this IServiceCollection services)
	{
		services.AddScoped<ILiveboxService, LiveboxService>();
		return services;
	}
}
