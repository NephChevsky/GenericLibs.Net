using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace RedisLib
{
	public static class RedisServiceCollectionExtensions
	{
		public static IServiceCollection AddRedisLib(this IServiceCollection services, IConfiguration configuration)
		{
			string connectionString = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Connection string 'Redis' was not found");

			services.AddSingleton<IConnectionMultiplexer>(sp =>
			{
				return ConnectionMultiplexer.Connect(connectionString);
			});

			services.AddSingleton<IRedisService, RedisService>();

			return services;
		}
	}
}
