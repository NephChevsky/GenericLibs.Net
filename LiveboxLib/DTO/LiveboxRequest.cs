namespace LiveboxLib.DTO;

public class LiveboxRequest(string service, string method, Dictionary<string, object> parameters)
{
	public string Service { get; set; } = service;
	public string Method { get; set; } = method;
	public Dictionary<string, object> Parameters { get; set; } = parameters;
}
