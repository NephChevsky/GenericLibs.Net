namespace CopilotLib.DTO
{
	public class ChatSessionInfo
	{
		public string SessionId { get; set; } = string.Empty;

		/// <summary>Display title of the session, falling back to the summary when none was generated.</summary>
		public string? Title { get; set; }

		public DateTimeOffset StartTime { get; set; }

		public DateTimeOffset ModifiedTime { get; set; }

		public string? Summary { get; set; }
	}

	public class ChatMessageInfo
	{
		/// <summary>"user" or "assistant".</summary>
		public string Role { get; set; } = string.Empty;

		public string Content { get; set; } = string.Empty;

		public DateTimeOffset Timestamp { get; set; }
	}
}
