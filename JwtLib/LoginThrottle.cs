using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace JwtLib
{
	/// <summary>
	/// In-memory sliding-window throttle for failed authentication attempts.
	/// Failures are tracked per account (username) and per client IP, so one attacker cannot lock every user out
	/// and one account cannot be brute-forced from many addresses. Settings (all optional, under <c>JwtSettings</c>):
	/// <c>LoginMaxFailuresPerUser</c> (default 5), <c>LoginMaxFailuresPerIp</c> (default 20), <c>LoginLockoutMinutes</c> (default 15).
	/// State is per process: with several instances, the effective limit is multiplied by the instance count.
	/// </summary>
	public class LoginThrottle(IConfiguration configuration) : ILoginThrottle
	{
		private const int MaxTrackedKeys = 50_000;

		private readonly int _maxPerUser = Math.Max(1, configuration.GetValue("JwtSettings:LoginMaxFailuresPerUser", 5));
		private readonly int _maxPerIp = Math.Max(1, configuration.GetValue("JwtSettings:LoginMaxFailuresPerIp", 20));
		private readonly TimeSpan _window = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("JwtSettings:LoginLockoutMinutes", 15)));

		private readonly ConcurrentDictionary<string, Queue<DateTime>> _failures = new();

		/// <summary>Returns false (and how long to wait) when the account or the client address has too many recent failures.</summary>
		public bool IsBlocked(string? username, string? ip, out TimeSpan retryAfter)
		{
			TimeSpan userWait = string.IsNullOrWhiteSpace(username) ? TimeSpan.Zero : RemainingBlock(UserKey(username), _maxPerUser);
			TimeSpan ipWait = RemainingBlock(IpKey(ip), _maxPerIp);
			retryAfter = userWait > ipWait ? userWait : ipWait;
			return retryAfter > TimeSpan.Zero;
		}

		/// <summary>Records a failure for the client address and, when known, for the account.</summary>
		public void RecordFailure(string? username, string? ip)
		{
			if (_failures.Count > MaxTrackedKeys)
			{
				Purge();
			}

			if (!string.IsNullOrWhiteSpace(username))
			{
				Add(UserKey(username));
			}
			Add(IpKey(ip));
		}

		/// <summary>A successful login resets the account counter (the IP counter keeps counting).</summary>
		public void RecordSuccess(string? username)
		{
			_failures.TryRemove(UserKey(username), out _);
		}

		private static string UserKey(string? username) => "u:" + (username ?? string.Empty).Trim().ToLowerInvariant();

		private static string IpKey(string? ip) => "ip:" + (ip ?? "unknown");

		private TimeSpan RemainingBlock(string key, int max)
		{
			if (!_failures.TryGetValue(key, out Queue<DateTime>? queue))
			{
				return TimeSpan.Zero;
			}

			lock (queue)
			{
				DateTime now = DateTime.UtcNow;
				while (queue.Count > 0 && now - queue.Peek() > _window)
				{
					queue.Dequeue();
				}

				// Blocked until the oldest failure that still counts leaves the window.
				return queue.Count >= max ? _window - (now - queue.Peek()) : TimeSpan.Zero;
			}
		}

		private void Add(string key)
		{
			Queue<DateTime> queue = _failures.GetOrAdd(key, _ => new Queue<DateTime>());
			lock (queue)
			{
				queue.Enqueue(DateTime.UtcNow);
				int cap = Math.Max(_maxPerUser, _maxPerIp);
				while (queue.Count > cap)
				{
					queue.Dequeue();
				}
			}
		}

		private void Purge()
		{
			DateTime now = DateTime.UtcNow;
			foreach (KeyValuePair<string, Queue<DateTime>> entry in _failures)
			{
				bool expired;
				lock (entry.Value)
				{
					expired = entry.Value.Count == 0 || now - entry.Value.Peek() > _window && now - entry.Value.Last() > _window;
				}

				if (expired)
				{
					_failures.TryRemove(entry.Key, out _);
				}
			}
		}
	}
}
