using LLMLib.DTO;
using Microsoft.Extensions.AI;

namespace LLMLib
{
	public interface ILLMService : IAsyncDisposable
	{
		/// <summary>Creates a new conversation and returns a session bound to it.</summary>
		Task<ILLMSession> CreateSessionAsync(LLMSessionOptions? options = null, CancellationToken cancellationToken = default);

		/// <summary>Resumes an existing conversation. Returns null if it does not exist.</summary>
		Task<ILLMSession?> OpenSessionAsync(string sessionId, LLMSessionOptions? options = null, CancellationToken cancellationToken = default);

		/// <summary>Lists all stored sessions.</summary>
		Task<IReadOnlyList<ChatSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default);

		/// <summary>Deletes a session and its stored data. Returns false if the session does not exist.</summary>
		Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
	}
}
