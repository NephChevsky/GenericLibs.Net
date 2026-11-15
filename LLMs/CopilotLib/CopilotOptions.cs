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
		/// When true, every permission request made by the agent is approved. When false (default), the restrictive default is used
		/// (public URLs and caller-provided custom tools only).
		/// </summary>
		public bool ApproveAllPermissions { get; set; }

		/// <summary>Answers the agent's ask_user questions. When null, the ask_user tool is not available to the agent.</summary>
		public Func<UserInputQuestion, Task<UserInputAnswer>>? UserInputHandler { get; set; }
	}

	/// <summary>A question the agent asks the user.</summary>
	public sealed record UserInputQuestion(string Question, IReadOnlyList<string> Choices, bool AllowFreeform);

	/// <summary>The user's answer to a <see cref="UserInputQuestion"/>.</summary>
	public sealed record UserInputAnswer(string Answer, bool WasFreeform);
}
