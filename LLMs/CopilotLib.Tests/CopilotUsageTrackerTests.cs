using Xunit;

namespace CopilotLib.Tests
{
	/// <summary>Verifies credit conversion and session cost accounting.</summary>
	public class CopilotUsageTrackerTests
	{
		[Fact]
		public void ToDollarsConvertsCredits()
		{
			Assert.Equal(2.5m, CopilotUsageTracker.ToDollars(250m));
		}

		[Fact]
		public void TracksSessionAndResponseCosts()
		{
			CopilotUsageTracker tracker = new(1m);
			LLMLib.DTO.LLMUsageUpdate first = tracker.AddCredits(10m);
			LLMLib.DTO.LLMUsageUpdate second = tracker.AddCredits(5m);
			Assert.Equal(new LLMLib.DTO.LLMUsageUpdate(0.10m, 1.10m), first);
			Assert.Equal(new LLMLib.DTO.LLMUsageUpdate(0.15m, 1.15m), second);
			Assert.Equal(1.15m, tracker.SessionCost);
			tracker.BeginResponse();
			Assert.Equal(new LLMLib.DTO.LLMUsageUpdate(0.03m, 1.18m), tracker.AddCredits(3m));
		}

		[Fact]
		public void ExtractsCreditsOnlyFromUsageEvents()
		{
			GitHub.Copilot.SessionEvent first = GitHub.Copilot.SessionEvent.FromJson("{\"type\":\"assistant.usage\",\"id\":\"00000000-0000-0000-0000-000000000001\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"data\":{\"model\":\"test-model\",\"copilotUsage\":{\"totalNanoAiu\":10000000000}}}");
			GitHub.Copilot.SessionEvent second = GitHub.Copilot.SessionEvent.FromJson("{\"type\":\"assistant.usage\",\"id\":\"00000000-0000-0000-0000-000000000002\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"data\":{\"model\":\"test-model\",\"copilotUsage\":{\"totalNanoAiu\":5000000000}}}");
			GitHub.Copilot.SessionEvent missing = GitHub.Copilot.SessionEvent.FromJson("{\"type\":\"assistant.usage\",\"id\":\"00000000-0000-0000-0000-000000000003\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"data\":{\"model\":\"test-model\"}}");
			GitHub.Copilot.SessionEvent idle = GitHub.Copilot.SessionEvent.FromJson("{\"type\":\"session.idle\",\"id\":\"00000000-0000-0000-0000-000000000004\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"data\":{}}");
			Assert.Equal(10m, CopilotUsageTracker.GetAiCredits(first));
			Assert.Equal(0m, CopilotUsageTracker.GetAiCredits(missing));
			Assert.Null(CopilotUsageTracker.GetAiCredits(idle));
			Assert.Equal(5m, CopilotUsageTracker.GetAiCredits(second));
		}

		[Theory]
		[InlineData(250000000000d, 2.5)]
		[InlineData(0d, 0)]
		[InlineData(null, 0)]
		public void ConvertsAccumulatedMetrics(double? nanoAiu, double expectedDollars)
		{
			Assert.Equal((decimal)expectedDollars, CopilotUsageTracker.ToDollarsFromNanoAiu(nanoAiu));
		}


		[Fact]
		public void ResumedMetricContinuesAccumulatingNewUsage()
		{
			decimal restoredCost = CopilotUsageTracker.ToDollarsFromNanoAiu(100000000000d);
			CopilotUsageTracker tracker = new(restoredCost);
			Assert.Equal(1m, tracker.SessionCost);
			Assert.Equal(new LLMLib.DTO.LLMUsageUpdate(0.1m, 1.1m), tracker.AddCredits(10m));
		}

	}
}
