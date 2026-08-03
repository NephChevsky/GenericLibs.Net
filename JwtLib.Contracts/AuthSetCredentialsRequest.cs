namespace JwtLib.DTO
{
	public class AuthSetCredentialsRequest
	{
		public required string Username { get; set; }
		public required string Password { get; set; }
	}
}
