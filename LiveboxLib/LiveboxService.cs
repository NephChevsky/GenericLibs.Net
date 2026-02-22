using LiveboxLib.DTO;
using System.Net.Http.Json;

namespace LiveboxLib;

public class LiveboxService : ILiveboxService
{
	public async Task ToggleRdpRule(string url, string user, string password, string rdpPort, string ipAddress, bool enabled)
	{
		HttpClient client = new()
		{
			BaseAddress = new Uri($"http://{url}/ws")
		};
		HttpRequestMessage request = new(HttpMethod.Post, "")
		{
			Content = JsonContent.Create(new LiveboxRequest("sah.Device.Information", "createContext", new() {
				{ "applicationName", "webui" },
				{ "username", user },
				{ "password", password }
			}))
		};
		request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-sah-ws-4-call+json");
		request.Headers.Add("Authorization", "X-Sah-Login");
		HttpResponseMessage response = await client.SendAsync(request);
		string? contextId = (await response.Content.ReadFromJsonAsync<LiveboxResponse>())?.Data["contextID"].ToString();

		if (enabled)
		{
			request = new(HttpMethod.Post, "")
			{
				Content = JsonContent.Create(new LiveboxRequest("Firewall", "setPortForwarding", new()
				{
					{ "id", "RDP" },
					{ "internalPort", "3389" },
					{ "externalPort", rdpPort },
					{ "destinationIPAddress", ipAddress },
					{ "enable", true },
					{ "persistent", true },
					{ "protocol", "6" },
					{ "description", "RDP" },
					{ "sourceInterface", "data" },
					{ "origin", "webui" },
					{ "destinationMACAddress", "" }
				}))
			};
		}
		else
		{
			request = new(HttpMethod.Post, "")
			{
				Content = JsonContent.Create(new LiveboxRequest("Firewall", "deletePortForwarding", new()
				{
					{ "id", "webui_RDP" },
					{ "destinationIPAddress", ipAddress },
					{ "origin", "webui" }
				}))
			};
		}

		request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-sah-ws-4-call+json");
		request.Headers.Add("Authorization", $"X-Sah {contextId}");
		request.Headers.Add("X-Content", contextId);

		response = await client.SendAsync(request);
		await response.Content.ReadFromJsonAsync<LiveboxResponse>();
	}
}
