using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;

namespace HangFireLib
{
	public static class HangFireServiceCollectionExtensions
	{
		public static IServiceCollection AddHangFireLib(this IServiceCollection services)
		{
			services.AddHangfire(config =>
			{
				config.UseSimpleAssemblyNameTypeSerializer()
					  .UseRecommendedSerializerSettings()
					  .UseMemoryStorage();
			});
			services.AddHangfireServer();

			return services;
		}
	}
}
