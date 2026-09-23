using System.Text.Json.Serialization;

namespace JwtLib.DTO
{
	/// <summary>
	/// Source-generated (reflection-free) JSON metadata for JwtLib.Contracts' DTOs. Consumers that
	/// run where reflection-based System.Text.Json serialization is unavailable or disabled (e.g.
	/// Browser/WASM clients) should use this context instead of the default reflection-based
	/// JsonSerializer overloads.
	/// </summary>
	[JsonSerializable(typeof(AuthLoginRequest))]
	[JsonSerializable(typeof(AuthLoginResponse))]
	[JsonSerializable(typeof(AuthUserResponse))]
	[JsonSerializable(typeof(AuthChangePasswordRequest))]
	[JsonSerializable(typeof(AuthChangeUsernameRequest))]
	[JsonSerializable(typeof(AuthGuestResponse))]
	[JsonSerializable(typeof(AuthRedeemGuestRequest))]
	[JsonSerializable(typeof(AuthSetCredentialsRequest))]
	public partial class JwtLibJsonContext : JsonSerializerContext
	{
	}
}
