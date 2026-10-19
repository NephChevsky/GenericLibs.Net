namespace CopilotLib.DTO
{
	public class ChatBotRequest
	{
		public string Message { get; set; } = string.Empty;

		/// <summary>Id of the conversation to resume. A new id is generated when omitted.</summary>
		public string? SessionId { get; set; }
	}

	public class ChatBotResponse
	{
		public string SessionId { get; set; } = string.Empty;

		public string Reply { get; set; } = string.Empty;
	}
}
