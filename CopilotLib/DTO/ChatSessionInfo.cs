namespace CopilotLib.DTO
{
	public class ChatSessionInfo
	{
		public string SessionId { get; set; } = string.Empty;

		public DateTimeOffset StartTime { get; set; }

		public DateTimeOffset ModifiedTime { get; set; }

		public string? Summary { get; set; }
	}

	public class ChatMessageInfo
	{
		public string Role { get; set; } = string.Empty;

		public string Content { get; set; } = string.Empty;

		public DateTimeOffset Timestamp { get; set; }

		public List<ChatAttachment> Attachments { get; set; } = [];
	}
}
