namespace JwtLib.DTO
{
	public class AuthChangePasswordRequest
	{
		public required string CurrentPassword { get; set; }
		public required string NewPassword { get; set; }
	}
}
