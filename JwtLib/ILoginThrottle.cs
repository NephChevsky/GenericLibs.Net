namespace JwtLib
{
	/// <summary>
	/// Throttles repeated failed authentication attempts. The default implementation (<see cref="LoginThrottle"/>)
	/// is in memory; register another one before <c>AddJwtAuthenticationLib</c> to replace it (e.g. a shared store for multi-instance setups).
	/// </summary>
	public interface ILoginThrottle
	{
		/// <summary>True (with how long to wait) when the account or the client address has too many recent failures.</summary>
		bool IsBlocked(string? username, string? ip, out TimeSpan retryAfter);

		/// <summary>Records a failure for the client address and, when known, for the account.</summary>
		void RecordFailure(string? username, string? ip);

		/// <summary>Resets the account counter after a successful authentication.</summary>
		void RecordSuccess(string? username);
	}
}
