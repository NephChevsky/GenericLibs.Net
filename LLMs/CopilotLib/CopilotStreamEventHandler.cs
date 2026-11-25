using System.Threading.Channels;
using GitHub.Copilot;
using LLMLib.DTO;

namespace CopilotLib
{
	/// <summary>Serializes SDK events and completes the response after a bounded idle grace period.</summary>
	internal sealed class CopilotStreamEventHandler : IDisposable
	{
		private readonly Lock _lock = new();
		private readonly CopilotUsageTracker _usage;
		private readonly TimeSpan _idleGracePeriod;
		private readonly CancellationTokenSource _idleCancellation = new();
		private readonly Channel<LLMStreamItem> _channel = Channel.CreateUnbounded<LLMStreamItem>();
		private bool _idle;
		private bool _completed;

		internal CopilotStreamEventHandler(CopilotUsageTracker usage, TimeSpan idleGracePeriod)
		{
			_usage = usage;
			_idleGracePeriod = idleGracePeriod;
		}

		internal ChannelReader<LLMStreamItem> Reader => _channel.Reader;

		internal void Handle(SessionEvent evt)
		{
			lock (_lock)
			{
				if (_completed)
				{
					return;
				}

				switch (evt)
				{
					case AssistantMessageDeltaEvent delta:
						_channel.Writer.TryWrite(new LLMTextChunk(delta.Data.DeltaContent));
						break;
					case SessionErrorEvent error:
						_completed = true;
						_channel.Writer.TryComplete(new InvalidOperationException(error.Data.Message));
						break;
					case SessionIdleEvent when !_idle:
						_idle = true;
						_ = CompleteAfterIdleAsync();
						break;
					default:
						if (CopilotUsageTracker.GetAiCredits(evt) is decimal credits)
						{
							_channel.Writer.TryWrite(_usage.AddCredits(credits));
						}
						break;
				}
			}
		}

		private async Task CompleteAfterIdleAsync()
		{
			try
			{
				await Task.Delay(_idleGracePeriod, _idleCancellation.Token).ConfigureAwait(false);
				lock (_lock)
				{
					if (!_completed)
					{
						_completed = true;
						_channel.Writer.TryComplete();
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		public void Dispose()
		{
			lock (_lock)
			{
				_completed = true;
				_channel.Writer.TryComplete();
			}
			_idleCancellation.Cancel();
			_idleCancellation.Dispose();
		}
	}
}
