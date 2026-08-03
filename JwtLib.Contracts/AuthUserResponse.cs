namespace JwtLib.DTO
{
	public class AuthUserResponse(Guid id, string name, string role, bool isGuest)
	{
		public Guid Id { get; set; } = id;
		public string Name { get; set; } = name;
		public string Role { get; set; } = role;
		public bool IsGuest { get; set; } = isGuest;
	}
}
