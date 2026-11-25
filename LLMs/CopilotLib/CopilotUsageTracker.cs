using GitHub.Copilot;
using LLMLib.DTO;

namespace CopilotLib
{
	/// <summary>Tracks the reported response and session cost in US dollars.</summary>
	internal sealed class CopilotUsageTracker
	{
		internal const decimal AiCreditsPerDollar = 100m;
		private readonly Lock _lock = new();
		private decimal _responseCost;
		private decimal _sessionCost;

		internal CopilotUsageTracker(decimal initialSessionCost)
		{
			_sessionCost = initialSessionCost;
		}

		internal static decimal ToDollars(decimal aiCredits) => aiCredits / AiCreditsPerDollar;

		internal decimal SessionCost
		{
			get
			{
				lock (_lock)
				{
					return _sessionCost;
				}
			}
		}

		internal void BeginResponse()
		{
			lock (_lock)
			{
				_responseCost = 0m;
			}
		}

		internal LLMUsageUpdate AddCredits(decimal aiCredits)
		{
			lock (_lock)
			{
				decimal cost = ToDollars(aiCredits);
				_responseCost += cost;
				_sessionCost += cost;
				return new LLMUsageUpdate(_responseCost, _sessionCost);
			}
		}

		internal static decimal? GetAiCredits(SessionEvent evt)
		{
			if (evt is not AssistantUsageEvent usage)
			{
				return null;
			}

			// SDK totalNanoAiu is in billionths of an AI credit; missing billing contributes zero and leaves a potentially incomplete subtotal.
			return (decimal)(usage.Data.CopilotUsage?.TotalNanoAiu ?? 0d) / 1_000_000_000m;
		}

		internal static decimal ToDollarsFromNanoAiu(double? nanoAiu) => ToDollars((decimal)(nanoAiu ?? 0d) / 1_000_000_000m);
	}
}
