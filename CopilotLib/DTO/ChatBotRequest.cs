namespace CopilotLib.DTO
{
	public class ChatBotRequest
	{
		public string Message { get; set; } = string.Empty;

		/// <summary>Id of the conversation to resume. A new id is generated when omitted.</summary>
		public string? SessionId { get; set; }

		/// <summary>Optional files/images sent along with the message.</summary>
		public List<ChatAttachment>? Attachments { get; set; }
	}

	public class ChatAttachment
	{
		public string Name { get; set; } = string.Empty;

		public string MimeType { get; set; } = "application/octet-stream";

		/// <summary>Base64-encoded content.</summary>
		public string Data { get; set; } = string.Empty;
	}

	public class ChatBotResponse
	{
		public string SessionId { get; set; } = string.Empty;

		public string Reply { get; set; } = string.Empty;
	}
}
