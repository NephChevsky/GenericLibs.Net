using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CopilotLib.DTO;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CopilotLib
{
	public class CopilotService : ICopilotService
	{
		private readonly ILogger<CopilotService> _logger;
		private readonly CopilotClient _client;
		private readonly string _defaultModel;
		private readonly SemaphoreSlim _startLock = new(1, 1);
		private readonly string _workingDirectory;
		private bool _started;

		public CopilotService(IConfiguration configuration, ILogger<CopilotService> logger)
		{
			_logger = logger;
			_defaultModel = "claude-sonnet5.5";

			string baseDirectory = Path.Combine(AppContext.BaseDirectory, "Copilot");
			Directory.CreateDirectory(baseDirectory);

			// Empty directory so the runtime does not inherit the host project folder as its working context.
			_workingDirectory = Path.Combine(baseDirectory, "workspace");
			Directory.CreateDirectory(_workingDirectory);

			CopilotClientOptions options = new() { Logger = logger, Mode = CopilotClientMode.Empty, BaseDirectory = baseDirectory, WorkingDirectory = _workingDirectory };
			string? token = configuration.GetConnectionString("Copilot");
			if (!string.IsNullOrWhiteSpace(token))
			{
				options.GitHubToken = token;
			}

			_client = new CopilotClient(options);
		}

		public async IAsyncEnumerable<string> AskStreamingAsync(string prompt, string? systemMessage = null, string? model = null, string? sessionId = null, IReadOnlyList<AIFunction>? tools = null, IReadOnlyList<ChatAttachment>? attachments = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await using CopilotSession session = await GetSessionAsync(systemMessage, model, sessionId, true, tools, cancellationToken);

			Channel<string> channel = Channel.CreateUnbounded<string>();
			using IDisposable subscription = session.On<SessionEvent>(evt =>
			{
				switch (evt)
				{
					case AssistantMessageDeltaEvent delta:
						channel.Writer.TryWrite(delta.Data.DeltaContent);
						break;
					case SessionErrorEvent error:
						channel.Writer.TryComplete(new InvalidOperationException(error.Data.Message));
						break;
					case SessionIdleEvent:
						channel.Writer.TryComplete();
						break;
				}
			});

			MessageOptions options = new() { Prompt = prompt };
			if (attachments is { Count: > 0 })
			{
				options.Attachments = [.. attachments.Select(a => (Attachment)new AttachmentBlob { Data = a.Data, MimeType = a.MimeType, DisplayName = a.Name })];
			}

			await session.SendAsync(options, cancellationToken);

			await foreach (string chunk in channel.Reader.ReadAllAsync(cancellationToken))
			{
				yield return chunk;
			}
		}

		public async Task<IReadOnlyList<ChatSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default)
		{
			await EnsureStartedAsync(cancellationToken);
			IList<SessionMetadata> sessions = await _client.ListSessionsAsync(cancellationToken: cancellationToken);
			List<ChatSessionInfo> result = [];
			foreach (SessionMetadata s in sessions.OrderByDescending(s => s.ModifiedTime))
			{
				result.Add(new ChatSessionInfo
				{
					SessionId = s.SessionId,
					StartTime = s.StartTime,
					ModifiedTime = s.ModifiedTime,
					Summary = s.Summary,
				});
			}

			return result;
		}

		public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
		{
			await EnsureStartedAsync(cancellationToken);
			IList<SessionMetadata> sessions = await _client.ListSessionsAsync();
			if (!sessions.Any(s => s.SessionId == sessionId))
			{
				return false;
			}

			await _client.DeleteSessionAsync(sessionId);
			_logger.LogInformation("Deleted Copilot session {SessionId}", sessionId);
			return true;
		}

		public async Task<IReadOnlyList<ChatMessageInfo>?> GetMessagesAsync(string sessionId, CancellationToken cancellationToken = default)
		{
			await EnsureStartedAsync(cancellationToken);

			await using CopilotSession session = await GetSessionAsync(null, null, sessionId, false, null, cancellationToken);

			if (session == null)
			{
				return null;
			}

			IReadOnlyList<SessionEvent> events = await session.GetEventsAsync(cancellationToken);

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

		private async Task<CopilotSession> GetSessionAsync(string? systemMessage, string? model, string? sessionId, bool streaming, IReadOnlyList<AIFunction>? tools, CancellationToken cancellationToken)
		{
			await EnsureStartedAsync(cancellationToken);

			List<AIFunctionDeclaration> customTools = tools is null ? [] : [.. tools];
			HashSet<string> customToolNames = [.. customTools.Select(t => t.Name)];
			List<string> availableTools = ["web_fetch", .. customToolNames];

			SystemMessageConfig? system = string.IsNullOrWhiteSpace(systemMessage)
				? null
				: new SystemMessageConfig { Mode = SystemMessageMode.Append, Content = systemMessage };

			// Text generation with web browsing only: no file or shell access.
			Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>> deny = (request, invocation) =>
				(request is PermissionRequestUrl url && IsPublicHttpUrl(url.Url)) || (request is PermissionRequestCustomTool custom && customToolNames.Contains(custom.ToolName))
					? PermissionHandler.ApproveAll(request, invocation)
					: Task.FromResult<PermissionDecision>(PermissionDecision.Reject("Tool use is not allowed"));

			if (!string.IsNullOrWhiteSpace(sessionId))
			{
				try
				{
					_logger.LogInformation("Resuming Copilot session {SessionId}", sessionId);
					return await _client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
					{
						Model = model ?? _defaultModel,
						Streaming = streaming,
						SystemMessage = system,
						WorkingDirectory = _workingDirectory,
						Tools = customTools,
						AvailableTools = availableTools,
						OnPermissionRequest = deny,
					}, cancellationToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					_logger.LogWarning(ex, "Failed to resume Copilot session {SessionId}, creating a new one", sessionId);
				}
			}

			return await _client.CreateSessionAsync(new SessionConfig
			{
				SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId,
				Model = model ?? _defaultModel,
				Streaming = streaming,
				SystemMessage = system,
				WorkingDirectory = _workingDirectory,
				Tools = customTools,
				AvailableTools = availableTools,
				OnPermissionRequest = deny,
			}, cancellationToken);
		}

		private static bool IsPublicHttpUrl(string? value)
		{
			if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
			{
				return false;
			}

			if (uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (System.Net.IPAddress.TryParse(uri.Host, out System.Net.IPAddress? ip))
			{
				if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
				{
					return false;
				}

				if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					byte[] b = ip.GetAddressBytes();
					if (b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168))
					{
						return false;
					}
				}
			}

			return true;
		}

		private async Task EnsureStartedAsync(CancellationToken cancellationToken)
		{
			if (_started)
			{
				return;
			}

			await _startLock.WaitAsync(cancellationToken);
			try
			{
				if (!_started)
				{
					await _client.StartAsync();
					_started = true;
				}
			}
			finally
			{
				_startLock.Release();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _client.DisposeAsync();
			_startLock.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
