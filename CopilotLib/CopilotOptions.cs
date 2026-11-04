using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace CopilotLib
{
	public class CopilotOptions
	{
		/// <summary>
		/// When true, the agent's built-in tools (shell, file read/write, ...) are available (Copilot CLI mode).
		/// When false (default), the session is restricted to text generation, web fetch and the tools passed by the caller.
		/// </summary>
		public bool EnableBuiltInTools { get; set; }

		/// <summary>
		/// Names of the built-in tools to expose when <see cref="EnableBuiltInTools"/> is true (e.g. "web_fetch").
		/// Caller-provided custom tools are always added. When null, every built-in tool is exposed.
		/// </summary>
		public IReadOnlyList<string>? AvailableBuiltInTools { get; set; }

		/// <summary>Directory the agent works in. Defaults to an empty directory under the application base directory.</summary>
		public string? WorkingDirectory { get; set; }

		/// <summary>
		/// Decides every permission request made by the agent. When null, the restrictive default is used
		/// (public URLs and caller-provided custom tools only).
		/// </summary>
		public Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>>? PermissionHandler { get; set; }
	}
}
