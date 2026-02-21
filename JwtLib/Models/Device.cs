using JwtLib.Interfaces;
using PostgresLib.Interfaces;

namespace JwtLib.Models
{
	public class Device : IJwtDevice, IDateTimeTrackable
	{
		public Guid Id { get; set; }
		public required string Name { get; set; }
		public Guid OwnerId { get; set; }
		public required string RefreshToken { get; set; }
		public DateTime? RefreshTokenExpiresAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}
}
