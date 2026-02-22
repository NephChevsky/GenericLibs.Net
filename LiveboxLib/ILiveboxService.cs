namespace LiveboxLib;

public interface ILiveboxService
{
	Task ToggleRdpRule(string url, string user, string password, string rdpPort, string ipAddress, bool enabled);
}
