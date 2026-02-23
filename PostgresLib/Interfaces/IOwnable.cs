namespace PostgresLib.Interfaces
{
	public interface IOwnable
	{
		public Guid Owner { get; set; }
	}
}
