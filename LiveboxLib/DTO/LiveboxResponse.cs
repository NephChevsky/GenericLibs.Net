namespace LiveboxLib.DTO;

public class LiveboxResponse
{
	public int Error { get; set; }
	public required string Description { get; set; }
	public required string Info { get; set; }
	public required Dictionary<string, object> Data { get; set; }
}
