namespace JwtLib.Interfaces
{
	public interface IJwtUser
	{
		Guid Id { get; set; }
		string Name { get; set; }
		string PasswordHash { get; set; }
		string Role { get; set; }
	}
}
