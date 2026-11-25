using GitHub.Copilot;
using LLMLib.DTO;
using Xunit;

namespace CopilotLib.Tests
{
	/// <summary>Verifies streaming event ordering and bounded completion.</summary>
	public class CopilotStreamEventHandlerTests
	{
		[Fact]
		public async Task UsageAfterIdleIsStreamedBeforeBoundedCompletion()
		{
			CopilotUsageTracker tracker = new(1m);
			using CopilotStreamEventHandler handler = new(tracker, TimeSpan.FromMilliseconds(200));
			handler.Handle(Event("session.idle", "{}"));
			Assert.False(handler.Reader.Completion.IsCompleted);
			handler.Handle(Event("assistant.usage", "{\"model\":\"test-model\",\"copilotUsage\":{\"totalNanoAiu\":10000000000}}"));
			LLMStreamItem item = await handler.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
			Assert.Equal(new LLMUsageUpdate(0.1m, 1.1m), item);
			await handler.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(2));
			handler.Handle(Event("assistant.usage", "{\"model\":\"test-model\",\"copilotUsage\":{\"totalNanoAiu\":5000000000}}"));
			Assert.Equal(1.1m, tracker.SessionCost);
		}


		[Fact]
		public async Task TextAndUsageRemainInOrder()
		{
			CopilotUsageTracker tracker = new(0m);
			using CopilotStreamEventHandler handler = new(tracker, TimeSpan.FromMilliseconds(20));
			handler.Handle(Event("assistant.message_delta", "{\"messageId\":\"m1\",\"deltaContent\":\"O\"}"));
			handler.Handle(Event("assistant.usage", "{\"model\":\"test-model\",\"copilotUsage\":{\"totalNanoAiu\":5000000000}}"));
			handler.Handle(Event("assistant.message_delta", "{\"messageId\":\"m1\",\"deltaContent\":\"K\"}"));
			handler.Handle(Event("session.idle", "{}"));
			Assert.Equal(new LLMTextChunk("O"), await handler.Reader.ReadAsync());
			Assert.Equal(new LLMUsageUpdate(0.05m, 0.05m), await handler.Reader.ReadAsync());
			Assert.Equal(new LLMTextChunk("K"), await handler.Reader.ReadAsync());
			await handler.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(2));
		}

		[Fact]
		public async Task IdleWithoutUsageStillCompletes()
		{
			using CopilotStreamEventHandler handler = new(new CopilotUsageTracker(0m), TimeSpan.FromMilliseconds(20));
			handler.Handle(Event("session.idle", "{}"));
			await handler.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(2));
		}

		[Fact]
		public async Task ErrorCompletesWithoutWaitingForIdleGrace()
		{
			using CopilotStreamEventHandler handler = new(new CopilotUsageTracker(0m), TimeSpan.FromSeconds(10));
			handler.Handle(Event("session.idle", "{}"));
			handler.Handle(Event("session.error", "{\"message\":\"failure\",\"errorType\":\"unknown\"}"));
			InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
			Assert.Equal("failure", error.Message);
		}

		[Fact]
		public async Task ConcurrentUsageEventsProduceConsistentUpdates()
		{
			CopilotUsageTracker tracker = new(0m);
			using CopilotStreamEventHandler handler = new(tracker, TimeSpan.FromMilliseconds(20));
			SessionEvent usage = Event("assistant.usage", "{\"model\":\"test-model\",\"copilotUsage\":{\"totalNanoAiu\":1000000000}}");
			Parallel.For(0, 100, i => handler.Handle(usage));
			handler.Handle(Event("session.idle", "{}"));
			for (int i = 1; i <= 100; i++)
			{
				Assert.Equal(new LLMUsageUpdate(i / 100m, i / 100m), await handler.Reader.ReadAsync());
			}
			await handler.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(2));
			Assert.Equal(1m, tracker.SessionCost);
		}

		private static SessionEvent Event(string type, string data)
		{
			return SessionEvent.FromJson($"{{\"type\":\"{type}\",\"id\":\"00000000-0000-0000-0000-000000000001\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"data\":{data}}}");
		}
	}
}
