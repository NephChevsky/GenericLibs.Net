using StackExchange.Redis;

namespace RedisLib
{
	public class RedisService : IRedisService
	{
		private readonly IConnectionMultiplexer _connectionMultiplexer;
		private readonly IDatabase _database;

		public RedisService(IConnectionMultiplexer connectionMultiplexer)
		{
			_connectionMultiplexer = connectionMultiplexer;
			_database = _connectionMultiplexer.GetDatabase();
		}

		public async Task<string?> StringGetAsync(string key)
		{
			RedisValue value = await _database.StringGetAsync(key);
			return value.HasValue ? value.ToString() : null;
		}

		public async Task<bool> StringSetAsync(string key, string value)
		{
			return await _database.StringSetAsync(key, value);
		}

		public async Task<bool> KeyDeleteAsync(string key)
		{
			return await _database.KeyDeleteAsync(key);
		}

		public IConnectionMultiplexer GetConnectionMultiplexer()
		{
			return _connectionMultiplexer;
		}
	}
}
