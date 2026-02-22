namespace LiveboxLib.DTO;

public class LiveboxResponse
{
	public int Error { get; set; }
	public string Description { get; set; }
	public string Info { get; set; }
	public Dictionary<string, object> Data { get; set; }
}
