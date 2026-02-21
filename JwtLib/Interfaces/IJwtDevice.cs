namespace JwtLib.Interfaces
{
	public interface IJwtDevice
	{
		Guid Id { get; set; }
		string Name { get; set; }
		Guid OwnerId { get; set; }
		string RefreshToken { get; set; }
		DateTime? RefreshTokenExpiresAt { get; set; }
	}
}
