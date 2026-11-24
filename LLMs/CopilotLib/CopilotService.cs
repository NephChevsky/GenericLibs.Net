using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using LLMLib;
using LLMLib.DTO;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CopilotLib
{
	public class CopilotService : ILLMService
	{
		private readonly ILogger<CopilotService> _logger;
		private readonly CopilotClient _client;
		private readonly string _defaultModel;
		private readonly SemaphoreSlim _startLock = new(1, 1);
		private readonly string _workingDirectory;
		private volatile bool _started;

		public CopilotService(IConfiguration configuration, ILogger<CopilotService> logger, CopilotOptions? copilotOptions = null)
		{
			_logger = logger;
			_defaultModel = "claude-sonnet5.5";

			string baseDirectory = Path.Combine(AppContext.BaseDirectory, "Copilot");
			Directory.CreateDirectory(baseDirectory);

			// By default an empty directory so the runtime does not inherit the host project folder as its working context.
			_workingDirectory = copilotOptions?.WorkingDirectory ?? Path.Combine(baseDirectory, "workspace");
			Directory.CreateDirectory(_workingDirectory);

			CopilotClientOptions options = new() { Logger = logger, Mode = CopilotClientMode.Empty, BaseDirectory = baseDirectory, WorkingDirectory = _workingDirectory };
			string? token = configuration.GetConnectionString("Copilot");
			if (!string.IsNullOrWhiteSpace(token))
			{
				options.GitHubToken = token;
			}

			_client = new CopilotClient(options);
		}

		public async Task<ILLMSession?> OpenSessionAsync(string sessionId, LLMSessionOptions? options = null, CancellationToken cancellationToken = default)
		{
			await EnsureStartedAsync(cancellationToken);
			IList<SessionMetadata> known = await _client.ListSessionsAsync(cancellationToken: cancellationToken);
			if (!known.Any(s => s.SessionId == sessionId))
			{
				return null;
			}

			CopilotSession session = await GetSessionAsync(options?.SystemMessage, options?.Model, options?.ReasoningEffort, sessionId, true, options?.Tools, false, cancellationToken);
			return new CopilotLLMSession(session, sessionId);
		}

		public async Task<ILLMSession> CreateSessionAsync(LLMSessionOptions? options = null, CancellationToken cancellationToken = default)
		{
			string sessionId = Guid.NewGuid().ToString();
			CopilotSession session = await GetSessionAsync(options?.SystemMessage, options?.Model, options?.ReasoningEffort, sessionId, true, options?.Tools, true, cancellationToken);
			_logger.LogInformation("Created Copilot session {SessionId}", sessionId);
			return new CopilotLLMSession(session, sessionId);
		}

		public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
		{
			await EnsureStartedAsync(cancellationToken);
			IList<SessionMetadata> sessions = await _client.ListSessionsAsync(cancellationToken: cancellationToken);
			if (!sessions.Any(s => s.SessionId == sessionId))
			{
				return false;
			}

			await _client.DeleteSessionAsync(sessionId);
			_logger.LogInformation("Deleted Copilot session {SessionId}", sessionId);
			return true;
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

		private async Task<CopilotSession> GetSessionAsync(string? systemMessage, string? model, string? reasoningEffort, string sessionId, bool streaming, IReadOnlyList<AIFunction>? tools, bool create, CancellationToken cancellationToken)
		{
			await EnsureStartedAsync(cancellationToken);

			// Built-in tools are never provided: the session can only use the caller's tools.
			List<AIFunctionDeclaration> customTools = tools is null ? [] : [.. tools];
			HashSet<string> customToolNames = [.. customTools.Select(t => t.Name)];
			List<string> availableTools = [.. customToolNames];

			SystemMessageConfig? system = string.IsNullOrWhiteSpace(systemMessage)
				? null
				: new SystemMessageConfig { Mode = SystemMessageMode.Append, Content = systemMessage };

			Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>> deny = (request, invocation) =>
				request is PermissionRequestCustomTool custom && customToolNames.Contains(custom.ToolName)
					? PermissionHandler.ApproveAll(request, invocation)
					: Task.FromResult<PermissionDecision>(PermissionDecision.Reject("Tool use is not allowed"));

			if (!create)
			{
				_logger.LogInformation("Resuming Copilot session {SessionId}", sessionId);
				return await _client.ResumeSessionAsync(sessionId, new ResumeSessionConfig
				{
					Model = model ?? _defaultModel,
					ReasoningEffort = reasoningEffort,
					Streaming = streaming,
					SystemMessage = system,
					WorkingDirectory = _workingDirectory,
					Tools = customTools,
					AvailableTools = availableTools,
					OnPermissionRequest = deny,
				}, cancellationToken);
			}

			return await _client.CreateSessionAsync(new SessionConfig
			{
				SessionId = sessionId,
				Model = model ?? _defaultModel,
				ReasoningEffort = reasoningEffort,
				Streaming = streaming,
				SystemMessage = system,
				WorkingDirectory = _workingDirectory,
				Tools = customTools,
				AvailableTools = availableTools,
				OnPermissionRequest = deny,
			}, cancellationToken);
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
