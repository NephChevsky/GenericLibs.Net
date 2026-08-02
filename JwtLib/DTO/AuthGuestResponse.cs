namespace JwtLib.DTO
{
	public class AuthGuestResponse(string token, string recoveryCode)
	{
		public string Token { get; set; } = token;
		public string RecoveryCode { get; set; } = recoveryCode;
	}
}
