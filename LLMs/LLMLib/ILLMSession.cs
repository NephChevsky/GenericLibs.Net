using LLMLib.DTO;
using Microsoft.Extensions.AI;

namespace LLMLib
{
	public class LLMSessionOptions
	{
		public string? SystemMessage { get; init; }
		public string? Model { get; init; }

		/// <summary>Reasoning effort requested from the model (e.g. "low", "medium", "high"). Provider default when null.</summary>
		public string? ReasoningEffort { get; init; }
		public IReadOnlyList<AIFunction>? Tools { get; init; }
	}

	/// <summary>A live conversation with an LLM. Obtain it via <see cref="ILLMService.CreateSessionAsync"/> or <see cref="ILLMService.OpenSessionAsync"/>. Dispose it when done.</summary>
	public interface ILLMSession : IAsyncDisposable
	{
		string SessionId { get; }

		/// <summary>Sends a prompt on this session and streams the response chunks as they are generated.</summary>
		IAsyncEnumerable<string> AskStreamingAsync(string prompt, IReadOnlyList<ChatAttachment>? attachments = null, CancellationToken cancellationToken = default);

		/// <summary>Returns the user and assistant messages of this session.</summary>
		Task<IReadOnlyList<ChatMessageInfo>> GetMessagesAsync(CancellationToken cancellationToken = default);
	}
}
