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
			await using ILLMSession? session = await _llm.OpenSessionAsync(sessionId, cancellationToken: cancellationToken);
			if (session is null)
			{
				return NotFound();
			}

			return Ok(await session.GetMessagesAsync(cancellationToken));
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

			LLMSessionOptions options = new() { SystemMessage = SystemMessage, Model = Model, Tools = Tools };
			ILLMSession? session;
			if (string.IsNullOrWhiteSpace(request.SessionId))
			{
				session = await _llm.CreateSessionAsync(options, cancellationToken);
			}
			else
			{
				session = await _llm.OpenSessionAsync(request.SessionId, options, cancellationToken);
				if (session is null)
				{
					Response.StatusCode = StatusCodes.Status404NotFound;
					return;
				}
			}

			await using ILLMSession _ = session;

			Response.Headers["X-Session-Id"] = session.SessionId;
			Response.Headers["Access-Control-Expose-Headers"] = "X-Session-Id";
			Response.Headers["X-Accel-Buffering"] = "no";
			Response.ContentType = "text/plain; charset=utf-8";

			await foreach (string chunk in session.AskStreamingAsync(string.IsNullOrWhiteSpace(request.Message) ? "See attachments" : request.Message, request.Attachments, cancellationToken))
			{
				await Response.WriteAsync(chunk, cancellationToken);
				await Response.Body.FlushAsync(cancellationToken);
			}
		}
	}
}
