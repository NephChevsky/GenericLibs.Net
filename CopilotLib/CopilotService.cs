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
		private readonly CopilotOptions _copilotOptions;
		private readonly CopilotClient _client;
		private readonly string _defaultModel;
		private readonly SemaphoreSlim _startLock = new(1, 1);
		private readonly string _workingDirectory;
		private volatile bool _started;

		public CopilotService(IConfiguration configuration, ILogger<CopilotService> logger, CopilotOptions? copilotOptions = null)
		{
			_logger = logger;
			_copilotOptions = copilotOptions ?? new CopilotOptions();
			_defaultModel = "claude-sonnet5.5";

			string baseDirectory = Path.Combine(AppContext.BaseDirectory, "Copilot");
			Directory.CreateDirectory(baseDirectory);

			// By default an empty directory so the runtime does not inherit the host project folder as its working context.
			_workingDirectory = _copilotOptions.WorkingDirectory ?? Path.Combine(baseDirectory, "workspace");
			Directory.CreateDirectory(_workingDirectory);

			CopilotClientMode mode = _copilotOptions.EnableBuiltInTools ? CopilotClientMode.CopilotCli : CopilotClientMode.Empty;
			CopilotClientOptions options = new() { Logger = logger, Mode = mode, BaseDirectory = baseDirectory, WorkingDirectory = _workingDirectory };
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

			CopilotSession session = await GetSessionAsync(options?.SystemMessage, options?.Model, sessionId, true, options?.Tools, false, cancellationToken);
			return new CopilotLLMSession(session, sessionId);
		}

		public async Task<ILLMSession> CreateSessionAsync(LLMSessionOptions? options = null, CancellationToken cancellationToken = default)
		{
			string sessionId = Guid.NewGuid().ToString();
			CopilotSession session = await GetSessionAsync(options?.SystemMessage, options?.Model, sessionId, true, options?.Tools, true, cancellationToken);
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

		private async Task<CopilotSession> GetSessionAsync(string? systemMessage, string? model, string sessionId, bool streaming, IReadOnlyList<AIFunction>? tools, bool create, CancellationToken cancellationToken)
		{
			await EnsureStartedAsync(cancellationToken);

			List<AIFunctionDeclaration> customTools = tools is null ? [] : [.. tools];
			HashSet<string> customToolNames = [.. customTools.Select(t => t.Name)];
			// Null exposes every built-in tool (only possible when built-in tools are explicitly enabled).
			List<string>? availableTools = !_copilotOptions.EnableBuiltInTools
				? ["web_fetch", .. customToolNames]
				: _copilotOptions.AvailableBuiltInTools is null
					? null
					: [.. _copilotOptions.AvailableBuiltInTools.Union(customToolNames)];

			SystemMessageConfig? system = string.IsNullOrWhiteSpace(systemMessage)
				? null
				: new SystemMessageConfig { Mode = SystemMessageMode.Append, Content = systemMessage };

			// Text generation with web browsing only: no file or shell access.
			Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>> deny = _copilotOptions.ApproveAllPermissions ? PermissionHandler.ApproveAll : ((request, invocation) =>
				(request is PermissionRequestUrl url && IsPublicHttpUrl(url.Url)) || (request is PermissionRequestCustomTool custom && customToolNames.Contains(custom.ToolName))
					? PermissionHandler.ApproveAll(request, invocation)
					: Task.FromResult<PermissionDecision>(PermissionDecision.Reject("Tool use is not allowed")));

			Func<UserInputRequest, UserInputInvocation, Task<UserInputResponse>>? userInput = null;
			if (_copilotOptions.UserInputHandler is { } handler)
			{
				userInput = async (request, _) =>
				{
					UserInputAnswer answer = await handler(new UserInputQuestion(request.Question, request.Choices is { Count: > 0 } ? [.. request.Choices] : [], request.AllowFreeform ?? true));
					return new UserInputResponse { Answer = answer.Answer, WasFreeform = answer.WasFreeform };
				};
			}

			if (!create)
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
					OnUserInputRequest = userInput,
				}, cancellationToken);
			}

			return await _client.CreateSessionAsync(new SessionConfig
			{
				SessionId = sessionId,
				Model = model ?? _defaultModel,
				Streaming = streaming,
				SystemMessage = system,
				WorkingDirectory = _workingDirectory,
				Tools = customTools,
				AvailableTools = availableTools,
				OnPermissionRequest = deny,
				OnUserInputRequest = userInput,
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
