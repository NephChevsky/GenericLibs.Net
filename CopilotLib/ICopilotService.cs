using Microsoft.Extensions.AI;

namespace CopilotLib
{
	public interface ICopilotService : IAsyncDisposable
	{
		/// <summary>
		/// Sends a prompt and streams the response chunks as they are generated. When <paramref name="sessionId"/> is provided,
		/// the matching conversation is resumed if it exists, otherwise a new one is created with that id.
		/// Without a session id, a one-off session is used.
		/// </summary>
		IAsyncEnumerable<string> AskStreamingAsync(string prompt, string? systemMessage = null, string? model = null, string? sessionId = null, IReadOnlyList<AIFunction>? tools = null, IReadOnlyList<CopilotLib.DTO.ChatAttachment>? attachments = null, CancellationToken cancellationToken = default);

		/// <summary>Lists all sessions stored in the base directory.</summary>
		Task<IReadOnlyList<CopilotLib.DTO.ChatSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default);

		/// <summary>Deletes a session and its stored data. Returns false if the session does not exist.</summary>
		Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

		/// <summary>Returns the user and assistant messages of a session, or null if the session does not exist.</summary>
		Task<IReadOnlyList<CopilotLib.DTO.ChatMessageInfo>?> GetMessagesAsync(string sessionId, CancellationToken cancellationToken = default);
	}
}
