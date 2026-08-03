namespace JwtLib.DTO
{
	public class AuthLoginRequest
	{
		public required string Username { get; set; }
		public required string Password { get; set; }
	}
}
