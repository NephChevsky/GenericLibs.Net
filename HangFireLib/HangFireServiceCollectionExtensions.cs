using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;

namespace HangFireLib
{
	public static class HangFireServiceCollectionExtensions
	{
		public static IServiceCollection AddHangFireLib<T>(this IServiceCollection services)
			where T : class
		{
			services.AddHangfire(config =>
			{
				config.UseSimpleAssemblyNameTypeSerializer()
					  .UseRecommendedSerializerSettings()
					  .UseMemoryStorage();
			});
			services.AddHangfireServer();

			// Register the service that contains recurring jobs
			services.AddScoped<T>();

			return services;
		}
	}
}
