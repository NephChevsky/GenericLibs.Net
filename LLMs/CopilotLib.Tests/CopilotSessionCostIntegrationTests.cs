using LLMLib;
using LLMLib.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CopilotLib.Tests
{
	/// <summary>Exercises streamed usage and session cost restoration against the live Copilot service.</summary>
	public class CopilotSessionCostIntegrationTests
	{
		[CopilotIntegrationFact]
		public async Task StreamsUsageAndRestoresSessionCost()
		{
			string token = Environment.GetEnvironmentVariable("COPILOT_TEST_TOKEN")!;
			IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Copilot"] = token,
			}).Build();
			await using CopilotService service = new(configuration, NullLogger<CopilotService>.Instance);
			ILLMSession? initial = null;
			ILLMSession? reopened = null;
			string? sessionId = null;
			try
			{
				LLMSessionOptions options = new() { Model = "gpt-4.1" };
				initial = await service.CreateSessionAsync(options);
				sessionId = initial.SessionId;
				List<LLMStreamItem> items = [];
				await foreach (LLMStreamItem item in initial.AskStreamingAsync("Reply with the single word OK"))
				{
					items.Add(item);
				}

				List<LLMTextChunk> text = items.OfType<LLMTextChunk>().ToList();
				List<LLMUsageUpdate> updates = items.OfType<LLMUsageUpdate>().ToList();
				Assert.NotEmpty(text);
				Assert.Contains("OK", string.Concat(text.Select(chunk => chunk.Text)), StringComparison.OrdinalIgnoreCase);
				Assert.NotEmpty(updates);
				LLMUsageUpdate last = updates[^1];
				Assert.Equal(last.SessionCost, initial.SessionCost);
				Assert.True(last.SessionCost >= last.ResponseCost);
				await initial.DisposeAsync();
				initial = null;

				reopened = await service.OpenSessionAsync(sessionId, options);
				Assert.NotNull(reopened);
				Assert.Equal(last.SessionCost, reopened.SessionCost);
			}
			finally
			{
				if (initial is not null)
				{
					await initial.DisposeAsync();
				}
				if (reopened is not null)
				{
					await reopened.DisposeAsync();
				}
				if (sessionId is not null)
				{
					await service.DeleteSessionAsync(sessionId);
				}
			}
		}
	}

	/// <summary>Skips live Copilot tests when no integration token is configured.</summary>
	public sealed class CopilotIntegrationFactAttribute : FactAttribute
	{
		public CopilotIntegrationFactAttribute()
		{
			if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPILOT_TEST_TOKEN")))
			{
				Skip = "COPILOT_TEST_TOKEN is not set.";
			}
		}
	}
}
