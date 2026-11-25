using System.Runtime.CompilerServices;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using LLMLib;
using LLMLib.DTO;

namespace CopilotLib
{
	/// <summary>Abstraction over a live <see cref="CopilotSession"/>. Created by <see cref="CopilotService"/>.</summary>
	public class CopilotLLMSession : ILLMSession
	{
		private readonly CopilotSession _session;
		private readonly CopilotUsageTracker _usage;
		private readonly SemaphoreSlim _responseLock = new(1, 1);
		private bool _disposed;

		internal CopilotLLMSession(CopilotSession session, string sessionId, decimal initialSessionCost)
		{
			_session = session;
			_usage = new CopilotUsageTracker(initialSessionCost);
			SessionId = sessionId;
		}

		public string SessionId { get; }

		/// <summary>Reported session cost in US dollars; it may be an incomplete subtotal when billing is unavailable.</summary>
		public decimal SessionCost => _usage.SessionCost;

		/// <summary>Streams text and reported usage; events arriving more than two seconds after idle are not included.</summary>
		public async IAsyncEnumerable<LLMStreamItem> AskStreamingAsync(string prompt, IReadOnlyList<ChatAttachment>? attachments = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await _responseLock.WaitAsync(cancellationToken);
			try
			{
				_usage.BeginResponse();
				// SDK documentation does not guarantee usage ordering relative to idle; allow a bounded grace period.
				using CopilotStreamEventHandler handler = new(_usage, TimeSpan.FromSeconds(2));
				using IDisposable subscription = _session.On<SessionEvent>(handler.Handle);

				MessageOptions messageOptions = new() { Prompt = prompt };
				if (attachments is { Count: > 0 })
				{
					messageOptions.Attachments = [.. attachments.Select(a => (Attachment)new AttachmentBlob { Data = a.Data, MimeType = a.MimeType, DisplayName = a.Name })];
				}

				await _session.SendAsync(messageOptions, cancellationToken);

				await foreach (LLMStreamItem item in handler.Reader.ReadAllAsync(cancellationToken))
				{
					yield return item;
				}
			}
			finally
			{
				_responseLock.Release();
			}
		}

		public async Task<IReadOnlyList<ChatMessageInfo>> GetMessagesAsync(CancellationToken cancellationToken = default)
		{
			IReadOnlyList<SessionEvent> events = await _session.GetEventsAsync(cancellationToken);

			Dictionary<string, SessionBinaryAssetData> assets = events.OfType<SessionBinaryAssetEvent>().GroupBy(e => e.Data.AssetId).ToDictionary(g => g.Key, g => g.First().Data);

			List<ChatMessageInfo> messages = [];
			foreach (SessionEvent evt in events)
			{
				switch (evt)
				{
					case UserMessageEvent user:
						ChatMessageInfo userMessage = new() { Role = "user", Content = user.Data.Content, Timestamp = user.Timestamp };
						foreach (AttachmentBlob blob in (user.Data.Attachments ?? []).OfType<AttachmentBlob>())
						{
							string? data = blob.Data;
							if (string.IsNullOrEmpty(data) && blob.AssetId is not null && assets.TryGetValue(blob.AssetId, out SessionBinaryAssetData? asset))
							{
								data = asset.Data;
							}

							if (!string.IsNullOrEmpty(data))
							{
								userMessage.Attachments.Add(new ChatAttachment { Name = blob.DisplayName ?? string.Empty, MimeType = blob.MimeType ?? "application/octet-stream", Data = data });
							}
						}

						messages.Add(userMessage);
						break;
					case AssistantMessageEvent assistant when !string.IsNullOrEmpty(assistant.Data.Content):
						messages.Add(new ChatMessageInfo { Role = "assistant", Content = assistant.Data.Content, Timestamp = assistant.Timestamp });
						break;
				}
			}

			return messages;
		}

		public async ValueTask DisposeAsync()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			await _session.DisposeAsync();
			GC.SuppressFinalize(this);
		}
	}
}
