namespace LLMLib.DTO
{
	/// <summary>Base type for items emitted while streaming an LLM response.</summary>
	public abstract record LLMStreamItem;

	/// <summary>Text generated during an LLM response.</summary>
	/// <param name="Text">The generated text.</param>
	public sealed record LLMTextChunk(string Text) : LLMStreamItem;

	/// <summary>An update emitted every time the provider reports usage. Costs are in US dollars.</summary>
	/// <param name="ResponseCost">The total cost for the current <see cref="LLMLib.ILLMSession.AskStreamingAsync"/> call so far.</param>
	/// <param name="SessionCost">The total cost for the whole session, including earlier turns and turns from before a resume.</param>
	public sealed record LLMUsageUpdate(decimal ResponseCost, decimal SessionCost) : LLMStreamItem;
}
