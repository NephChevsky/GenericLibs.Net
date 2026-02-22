using StackExchange.Redis;

namespace RedisLib
{
	public interface IRedisService
	{
		Task<string?> StringGetAsync(string key);
		Task<bool> StringSetAsync(string key, string value);
		Task<bool> KeyDeleteAsync(string key);
		IConnectionMultiplexer GetConnectionMultiplexer();
	}
}
