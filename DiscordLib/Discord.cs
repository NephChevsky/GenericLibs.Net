using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotifierLib;
using System.Net.Http.Json;

namespace DiscordLib
{
    public class Discord(IConfiguration configuration, ILogger<Discord> logger) : INotifier
    {
        private readonly ILogger<Discord> _logger = logger;
        private readonly string _webhookUrl = configuration.GetSection("Discord").GetValue<string>("WebHookUrl") ?? throw new InvalidOperationException("Discord WebHookUrl is not configured");
        private readonly string _adminUserId = configuration.GetSection("Discord").GetValue<string>("AdminUserId") ?? throw new InvalidOperationException("Discord AdminUserId is not configured");

        public async Task SendNotification(string message)
        {
            _logger.LogInformation("Sending Discord notification {Content}", message);
            string content = $"<@{_adminUserId}> {message}";
            using HttpClient client = new();
            HttpResponseMessage response = await client.PostAsJsonAsync(_webhookUrl, new { content });
            response.EnsureSuccessStatusCode();
        }
    }
}
