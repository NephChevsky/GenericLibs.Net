using LLMLib.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace LLMLib.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public abstract class ChatBotController(ILLMService llm) : ControllerBase
	{
		protected readonly ILLMService _llm = llm;

		/// <summary>Optional system message applied to every conversation.</summary>
		protected virtual string? SystemMessage => null;

		/// <summary>Optional model override. Falls back to the provider's default.</summary>
		protected virtual string? Model => null;

		/// <summary>Optional custom tools the assistant may call. Evaluated per request, so tools can capture request-scoped state (e.g. the current user).</summary>
		protected virtual IReadOnlyList<AIFunction>? Tools => null;

		[HttpGet("sessions")]
		public async Task<ActionResult<IReadOnlyList<ChatSessionInfo>>> GetSessions(CancellationToken cancellationToken)
		{
			return Ok(await _llm.GetSessionsAsync(cancellationToken));
		}

		[HttpGet("sessions/{sessionId}/messages")]
		public async Task<ActionResult<IReadOnlyList<ChatMessageInfo>>> GetMessages(string sessionId, CancellationToken cancellationToken)
		{
			IReadOnlyList<ChatMessageInfo>? messages = await _llm.GetMessagesAsync(sessionId, cancellationToken);
			return messages is null ? NotFound() : Ok(messages);
		}

		[HttpDelete("sessions/{sessionId}")]
		public async Task<IActionResult> DeleteSession(string sessionId, CancellationToken cancellationToken)
		{
			return await _llm.DeleteSessionAsync(sessionId, cancellationToken) ? NoContent() : NotFound();
		}

		[HttpPost("stream")]
		public async Task ChatStream([FromBody] ChatBotRequest request, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(request.Message) && request.Attachments is not { Count: > 0 })
			{
				Response.StatusCode = StatusCodes.Status400BadRequest;
				await Response.WriteAsync("Message is required", cancellationToken);
				return;
			}

			string sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId;
			Response.Headers["X-Session-Id"] = sessionId;
			Response.Headers["Access-Control-Expose-Headers"] = "X-Session-Id";
			Response.Headers["X-Accel-Buffering"] = "no";
			Response.ContentType = "text/plain; charset=utf-8";

			await foreach (string chunk in _llm.AskStreamingAsync(string.IsNullOrWhiteSpace(request.Message) ? "See attachments" : request.Message, SystemMessage, Model, sessionId, Tools, request.Attachments, cancellationToken))
			{
				await Response.WriteAsync(chunk, cancellationToken);
				await Response.Body.FlushAsync(cancellationToken);
			}
		}
	}
}
