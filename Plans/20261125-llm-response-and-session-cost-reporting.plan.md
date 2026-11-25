# Plan: LLM response and session cost reporting

Repository: `D:\dev\GenericLibs.Net`

## TL;DR

`ILLMSession.AskStreamingAsync` will return typed stream items instead of plain strings: `LLMTextChunk` for text and `LLMUsageUpdate` for cost. A new `SessionCost` property gives the running total. This is a breaking change. CopilotLib emits an `LLMUsageUpdate` every time the Copilot SDK reports a usage event. Each update carries the cost of the current response so far and the session total, in dollars, where $ = AI credits / 100. When a session is resumed, its total is rebuilt from the stored session events. `ChatBotController` is adapted to keep streaming text only. Versions are bumped (LLMLib 0.3.0, CopilotLib 0.7.0), a CopilotLib.Tests project is added, and both packages are packed into local-nuget-feed so CodeAssistant can consume them.

## Findings

- `ILLMSession.AskStreamingAsync` currently returns `IAsyncEnumerable<string>`. Callers: `ChatBotController` (LLMLib), plus CodeAssistant's TUI, WebApp `ChatConversation` and `FeaturePlanner`.
- `CopilotLLMSession.AskStreamingAsync` subscribes with `_session.On<SessionEvent>` and only handles `AssistantMessageDeltaEvent`, `SessionErrorEvent` and `SessionIdleEvent`. Nothing reads usage or cost events today.
- `CopilotService.OpenSessionAsync` resumes through `_client.ResumeSessionAsync`. `CopilotLLMSession.GetMessagesAsync` already shows how stored events are replayed with `_session.GetEventsAsync()`.
- CopilotLib consumes LLMLib as a NuGet package (`Neph.GenericLibs.Net.LLMLib`), with its version pinned centrally in Directory.Packages.props (0.2.0). So LLMLib 0.3.0 must be packed into local-nuget-feed before CopilotLib can build against it.
- Current versions are LLMLib 0.2.0 and CopilotLib 0.6.0. Packages are published to nuget.org by manual workflow_dispatch workflows and also stored in local-nuget-feed/.
- The repository has no test projects. The code style uses tabs, Allman braces, explicit types and XML doc summaries.

## Phases & tasks

### LLMLib API

Define the typed stream items and the new session contract in LLMLib, adapt `ChatBotController`, and bump LLMLib to 0.3.0.

| Id | Status | Task | Files | Depends on | Parallel with | Criteria |
|----|--------|------|-------|------------|---------------|----------|
| T1.1 | [ ] | **Add typed stream item DTOs**<br>Create LLMs/LLMLib/DTO/LLMStreamItem.cs in namespace `LLMLib.DTO` with:<br>- `public abstract record LLMStreamItem;`<br>- `public sealed record LLMTextChunk(string Text) : LLMStreamItem;`<br>- `public sealed record LLMUsageUpdate(decimal ResponseCost, decimal SessionCost) : LLMStreamItem;`<br><br>Add XML docs:<br>- Costs are in US dollars.<br>- `ResponseCost` is the total for the current `AskStreamingAsync` call so far.<br>- `SessionCost` is the total for the whole session, including earlier turns and turns from before a resume.<br>- An update is emitted every time the provider reports usage.<br><br>Follow the repository style (tabs, block namespace). | `LLMs/LLMLib/DTO/LLMStreamItem.cs` | — | — | AC-01, AC-02 |
| T1.2 | [ ] | **Change ILLMSession contract**<br>In LLMs/LLMLib/ILLMSession.cs:<br>- Change `AskStreamingAsync` to return `IAsyncEnumerable<LLMStreamItem>`. Keep the same parameters.<br>- Update its summary: it streams `LLMTextChunk` items as text is generated, and an `LLMUsageUpdate` each time the provider reports usage.<br>- Add `decimal SessionCost { get; }` with the summary "Total cost of this session in US dollars, including turns made before it was resumed." | `LLMs/LLMLib/ILLMSession.cs` | T1.1 | — | AC-01, AC-02, AC-03 |
| T1.3 | [ ] | **Adapt ChatBotController to typed items**<br>In LLMs/LLMLib/Controllers/ChatBotController.cs `ChatStream`:<br>- Iterate `IAsyncEnumerable<LLMStreamItem>`.<br>- For `LLMTextChunk text`, write `text.Text` and flush, exactly as before.<br>- Ignore every other item type, including `LLMUsageUpdate`.<br><br>Response headers and status codes stay unchanged. | `LLMs/LLMLib/Controllers/ChatBotController.cs` | T1.2 | — | AC-04 |
| T1.4 | [ ] | **Bump LLMLib to 0.3.0**<br>Make these version changes:<br>- In LLMs/LLMLib/LLMLib.csproj, set `<Version>0.3.0</Version>`.<br>- In Directory.Packages.props, set `Neph.GenericLibs.Net.LLMLib` to `0.3.0` so CopilotLib consumes the new API. | `LLMs/LLMLib/LLMLib.csproj`, `Directory.Packages.props` | — | T1.1, T1.2, T1.3 | AC-05 |

### CopilotLib cost tracking

Turn Copilot SDK usage events into `LLMUsageUpdate` items, track the session total including history on resume, and bump CopilotLib to 0.7.0.

| Id | Status | Task | Files | Depends on | Parallel with | Criteria |
|----|--------|------|-------|------------|---------------|----------|
| T2.1 | [ ] | **Confirm SDK usage event and add CopilotUsageTracker**<br>First, inspect GitHub.Copilot.SDK 1.0.34 (IntelliSense, or the package XML docs under the NuGet cache) and identify the session event that reports billing for each model call. Most likely this is `AssistantUsageEvent` ("assistant.usage"). Find the field that holds AI credits or nano-AIU, and its unit. Record the conversion in a code comment, for example `credits = nanoAiu / 1_000_000_000m`.<br><br>Then create LLMs/CopilotLib/CopilotUsageTracker.cs, an `internal sealed class CopilotUsageTracker` with:<br>- `internal const decimal AiCreditsPerDollar = 100m`.<br>- `internal static decimal ToDollars(decimal aiCredits) => aiCredits / AiCreditsPerDollar`.<br>- `internal static decimal? GetAiCredits(SessionEvent evt)`. It returns the credits of a usage event, `0` when the event is a usage event with a null field, and `null` for non-usage events.<br>- `internal static decimal SumSessionCost(IEnumerable<SessionEvent> events)`. It returns the dollar total of all usage events.<br>- A constructor `CopilotUsageTracker(decimal initialSessionCost)`.<br>- A thread-safe `decimal SessionCost` property (use a `Lock`; decimals can't use `Interlocked`).<br>- `void BeginResponse()`, which resets the response total to 0.<br>- `LLMUsageUpdate AddCredits(decimal aiCredits)`. It adds `ToDollars(aiCredits)` to both the response and session totals and returns `new LLMUsageUpdate(responseCost, sessionCost)`.<br><br>Add `<InternalsVisibleTo Include="CopilotLib.Tests" />` to CopilotLib.csproj. | `LLMs/CopilotLib/CopilotUsageTracker.cs`, `LLMs/CopilotLib/CopilotLib.csproj` | T1.1, T1.4 | — | AC-02, AC-03 |
| T2.2 | [ ] | **Stream typed items from CopilotLLMSession**<br>In LLMs/CopilotLib/CopilotLLMSession.cs:<br>- Add a `CopilotUsageTracker _usage` field. Change the internal constructor to `CopilotLLMSession(CopilotSession session, string sessionId, decimal initialSessionCost)`.<br>- Implement `public decimal SessionCost => _usage.SessionCost;`.<br>- In `AskStreamingAsync` (now returning `IAsyncEnumerable<LLMStreamItem>`), use `Channel<LLMStreamItem>` and call `_usage.BeginResponse()` before subscribing.<br>- In the event handler:<br>  - `AssistantMessageDeltaEvent`: write `new LLMTextChunk(delta.Data.DeltaContent)`.<br>  - Any event where `CopilotUsageTracker.GetAiCredits(evt)` is not null: write `_usage.AddCredits(credits)`.<br>  - Keep the existing error and idle handling.<br><br>Usage events that arrive after `SessionIdleEvent` aren't delivered for that call, since the subscription is disposed. If the SDK sends the usage event after idle, complete the channel only once both have arrived, or document the behaviour after checking the event order in T2.1.<br><br>`GetMessagesAsync` and `DisposeAsync` stay unchanged. | `LLMs/CopilotLib/CopilotLLMSession.cs` | T1.2, T2.1 | T2.3 | AC-01, AC-02 |
| T2.3 | [ ] | **Rebuild session cost on resume**<br>In LLMs/CopilotLib/CopilotService.cs:<br>- `CreateSessionAsync`: construct `new CopilotLLMSession(session, sessionId, 0m)`.<br>- `OpenSessionAsync`: after `GetSessionAsync`, call `IReadOnlyList<SessionEvent> events = await session.GetEventsAsync(cancellationToken)`. Then construct `new CopilotLLMSession(session, sessionId, CopilotUsageTracker.SumSessionCost(events))`.<br><br>Log the restored cost at Debug level (session id and cost). | `LLMs/CopilotLib/CopilotService.cs` | T2.1 | T2.2 | AC-03 |
| T2.4 | [ ] | **Bump CopilotLib to 0.7.0**<br>In LLMs/CopilotLib/CopilotLib.csproj, set `<Version>0.7.0</Version>`. | `LLMs/CopilotLib/CopilotLib.csproj` | T2.1 | T2.2, T2.3 | AC-05 |

### Tests and packaging

Cover the cost logic with tests and publish both packages to the local feed for CodeAssistant.

| Id | Status | Task | Files | Depends on | Parallel with | Criteria |
|----|--------|------|-------|------------|---------------|----------|
| T3.1 | [ ] | **Create CopilotLib.Tests project**<br>Create LLMs/CopilotLib.Tests/CopilotLib.Tests.csproj:<br>- net10.0, `IsPackable=false`, `NoWarn GHCP001`.<br>- A `ProjectReference` to ../CopilotLib/CopilotLib.csproj.<br>- Packages `Microsoft.NET.Test.Sdk`, `xunit.v3` (or `xunit` 2.x) and `xunit.runner.visualstudio`, with their versions added to Directory.Packages.props.<br>- Add the project to GenericLibs.Net.slnx under the /LLMs/ folder.<br><br>Add CopilotUsageTrackerTests.cs with these unit tests:<br>- (a) `ToDollars(250m) == 2.5m`.<br>- (b) A new tracker with initial cost 1.00: `AddCredits(10)` then `AddCredits(5)` gives updates (0.10, 1.10) and (0.15, 1.15), and `SessionCost == 1.15`.<br>- (c) `BeginResponse()` resets `ResponseCost` but not `SessionCost`.<br>- (d) `SumSessionCost` over a list with two usage events and unrelated events returns the dollar sum of the usage events only. Build the SDK event instances directly if their constructors are public, otherwise deserialize them from JSON.<br><br>Add CopilotSessionCostIntegrationTests.cs. It is skipped unless the `COPILOT_TEST_TOKEN` environment variable is set. It builds a `CopilotService` with that token as `ConnectionStrings:Copilot`, then:<br>- creates a session and asks "Reply with the single word OK";<br>- asserts that at least one `LLMTextChunk` and at least one `LLMUsageUpdate` were streamed;<br>- asserts that the last update's `SessionCost` equals `session.SessionCost` and is >= its `ResponseCost`;<br>- disposes the session, reopens it with `OpenSessionAsync`, and asserts that `SessionCost` equals the previous total;<br>- deletes the session. | `LLMs/CopilotLib.Tests/CopilotLib.Tests.csproj`, `LLMs/CopilotLib.Tests/CopilotUsageTrackerTests.cs`, `LLMs/CopilotLib.Tests/CopilotSessionCostIntegrationTests.cs`, `Directory.Packages.props`, `GenericLibs.Net.slnx` | T2.2, T2.3, T3.2 | — | AC-01, AC-02, AC-03 |
| T3.2 | [ ] | **Pack LLMLib then CopilotLib into local-nuget-feed**<br>From the repository root, run these commands in order:<br>1. `dotnet pack LLMs/LLMLib/LLMLib.csproj -c Release -o local-nuget-feed`<br>2. `dotnet restore LLMs/CopilotLib/CopilotLib.csproj`, which picks up LLMLib 0.3.0 from the local feed.<br>3. `dotnet pack LLMs/CopilotLib/CopilotLib.csproj -c Release -o local-nuget-feed`<br><br>This produces Neph.GenericLibs.Net.LLMLib.0.3.0.nupkg and Neph.GenericLibs.Net.CopilotLib.0.7.0.nupkg; commit them like the existing packages. Publishing to nuget.org is done separately through the workflows publish-llmlib-nuget.yml and publish-copilotlib-nuget.yml (LLMLib first). The CodeAssistant plan depends on this task. | `local-nuget-feed/Neph.GenericLibs.Net.LLMLib.0.3.0.nupkg`, `local-nuget-feed/Neph.GenericLibs.Net.CopilotLib.0.7.0.nupkg` | T1.3, T1.4, T2.2, T2.3, T2.4 | — | AC-05 |

## Acceptance criteria

### AC-01

Given a Copilot session, when `AskStreamingAsync` is enumerated, then the reply text arrives as `LLMTextChunk` items in generation order, and joining their `Text` gives the full reply.

| Test | Kind | Description | How to run | Expected result |
|------|------|-------------|------------|-----------------|
| AT-01.1 | Integration | `CopilotSessionCostIntegrationTests`: ask "Reply with the single word OK" and collect the `LLMTextChunk` items. | $env:COPILOT_TEST_TOKEN='<token>'; dotnet test LLMs/CopilotLib.Tests | At least one `LLMTextChunk` is received, and the joined text contains "OK". |

### AC-02

Given a session with cost C0, when the provider reports a usage event worth X AI credits during a response, then an `LLMUsageUpdate` is streamed immediately. Its `ResponseCost` is the dollar total of this response so far (X/100 summed) and its `SessionCost` is C0 plus `ResponseCost`. `session.SessionCost` matches it.

| Test | Kind | Description | How to run | Expected result |
|------|------|-------------|------------|-----------------|
| AT-02.1 | Unit | `CopilotUsageTrackerTests`: check the conversion, accumulation across several `AddCredits` calls, and the `BeginResponse` reset. | dotnet test LLMs/CopilotLib.Tests --filter FullyQualifiedName~CopilotUsageTrackerTests | `ToDollars(250)` = 2.5. The updates are (0.10, 1.10) and (0.15, 1.15). After `BeginResponse`, `ResponseCost` restarts at 0 while `SessionCost` is kept. All tests pass. |
| AT-02.2 | Integration | Live session: check that usage updates are streamed and consistent. | $env:COPILOT_TEST_TOKEN='<token>'; dotnet test LLMs/CopilotLib.Tests --filter FullyQualifiedName~CopilotSessionCostIntegrationTests | At least one `LLMUsageUpdate` is received. The last update's `SessionCost` equals `session.SessionCost` and is >= its `ResponseCost`. |

### AC-03

Given a stored session whose earlier turns cost C, when it is reopened with `OpenSessionAsync`, then `SessionCost` equals C before any new prompt, and later updates continue from C.

| Test | Kind | Description | How to run | Expected result |
|------|------|-------------|------------|-----------------|
| AT-03.1 | Unit | `SumSessionCost` over a list with usage events and unrelated events. | dotnet test LLMs/CopilotLib.Tests --filter FullyQualifiedName~CopilotUsageTrackerTests | Only the usage events are summed, converted to dollars. |
| AT-03.2 | Integration | Create a session, ask once, dispose it, reopen it with `OpenSessionAsync`, and compare `SessionCost`. | $env:COPILOT_TEST_TOKEN='<token>'; dotnet test LLMs/CopilotLib.Tests --filter FullyQualifiedName~CopilotSessionCostIntegrationTests | The reopened session's `SessionCost` equals the total reported by the last update before disposal. |

### AC-04

Given a controller derived from `ChatBotController`, when POST api/{controller}/stream is called, then the response body still contains only the reply text (no cost data), and the `X-Session-Id` header is still set.

| Test | Kind | Description | How to run | Expected result |
|------|------|-------------|------------|-----------------|
| AT-04.1 | Manual | Build the solution, then check the code and, in any host app deriving `ChatBotController`, call the stream endpoint. | dotnet build GenericLibs.Net.slnx; then POST {"message":"hi"} to /api/<controller>/stream | The build succeeds. The streamed body is plain reply text with no cost data, and the `X-Session-Id` header is present. |

### AC-05

Given the changes are complete, when the packages are packed, then local-nuget-feed contains Neph.GenericLibs.Net.LLMLib 0.3.0 and Neph.GenericLibs.Net.CopilotLib 0.7.0, and CopilotLib 0.7.0 depends on LLMLib 0.3.0.

| Test | Kind | Description | How to run | Expected result |
|------|------|-------------|------------|-----------------|
| AT-05.1 | Manual | Run the pack commands from T3.2 and inspect the nuspec dependencies of the CopilotLib package. | dotnet pack LLMs/LLMLib/LLMLib.csproj -c Release -o local-nuget-feed; dotnet pack LLMs/CopilotLib/CopilotLib.csproj -c Release -o local-nuget-feed; open the .nupkg (zip) and read the .nuspec | Both .nupkg files exist, and the CopilotLib nuspec lists the dependency Neph.GenericLibs.Net.LLMLib >= 0.3.0. |

## Relevant files

- `LLMs/LLMLib/ILLMSession.cs` (modify) — Change the stream type and add `SessionCost`.
- `LLMs/LLMLib/DTO/LLMStreamItem.cs` (create) — Typed stream items (text chunk and usage update).
- `LLMs/LLMLib/Controllers/ChatBotController.cs` (modify) — Write only text items to the response.
- `LLMs/LLMLib/LLMLib.csproj` (modify) — Version 0.3.0.
- `Directory.Packages.props` (modify) — Pin LLMLib 0.3.0 and add the test package versions.
- `LLMs/CopilotLib/CopilotUsageTracker.cs` (create) — Converts AI credits to dollars, accumulates totals, and sums the cost history.
- `LLMs/CopilotLib/CopilotLLMSession.cs` (modify) — Streams typed items and emits usage updates.
- `LLMs/CopilotLib/CopilotService.cs` (modify) — Passes the initial session cost (0 for new sessions, rebuilt history on resume).
- `LLMs/CopilotLib/CopilotLib.csproj` (modify) — Version 0.7.0 and InternalsVisibleTo for the tests.
- `LLMs/CopilotLib.Tests/CopilotLib.Tests.csproj` (create) — New test project.
- `LLMs/CopilotLib.Tests/CopilotUsageTrackerTests.cs` (create) — Unit tests for the cost logic.
- `LLMs/CopilotLib.Tests/CopilotSessionCostIntegrationTests.cs` (create) — Integration tests that use a live Copilot token and are skipped without one.
- `GenericLibs.Net.slnx` (modify) — Registers the test project.
- `local-nuget-feed` (reference) — Destination of the packed 0.3.0 and 0.7.0 packages.
- `.github/workflows/publish-llmlib-nuget.yml` (reference) — Manual publication of LLMLib to nuget.org.
- `.github/workflows/publish-copilotlib-nuget.yml` (reference) — Manual publication of CopilotLib to nuget.org.

## Decisions

- Cost is GitHub AI credits converted to dollars, using $ = AI credits / 100 (the user's definition).
- Breaking API change (the user chose it): `AskStreamingAsync` returns `IAsyncEnumerable<LLMStreamItem>`. `LLMStreamItem` is an abstract record with two sealed subtypes: `LLMTextChunk(string Text)` and `LLMUsageUpdate(decimal ResponseCost, decimal SessionCost)`.
- An `LLMUsageUpdate` is emitted for each usage event the provider reports, so a response that makes several model calls (tool loops) produces several updates. `ResponseCost` is the total for the current `AskStreamingAsync` call so far. `SessionCost` is the total of earlier turns plus `ResponseCost`.
- `ILLMSession` gets `decimal SessionCost { get; }` (read-only), so callers can show the total before the first reply, for example for resumed sessions.
- A session resumed with `OpenSessionAsync` starts from the sum of all usage events in its stored history (the user chose this). New sessions start at 0.
- `ChatBotController` keeps streaming text only and returns no cost (the user chose this).
- Version bumps: LLMLib 0.3.0 (breaking, minor bump while below 1.0) and CopilotLib 0.7.0.
- The usage-to-dollar arithmetic lives in an internal `CopilotUsageTracker` in CopilotLib, so it can be unit-tested without a live Copilot connection.

## Out of scope

- Showing or returning cost in `ChatBotController` responses.
- Cost reporting in OpenAILib (it doesn't implement `ILLMService`).
- Configurable price tables, token-count reporting and quota snapshots.
- Adding cost to `ChatSessionInfo` or to `GetSessionsAsync` listings.
- Publishing to nuget.org (manual workflow runs).

## Further considerations

1. The exact SDK usage event and the AI-credit field and unit must be confirmed against GitHub.Copilot.SDK 1.0.34 (T2.1). If the SDK only exposes a premium-request multiplier, the AI-credit conversion needs to be revisited with the user.
2. If usage events can arrive after `SessionIdleEvent`, the channel-completion logic in T2.2 must wait for them; otherwise the last update of a turn would only show up in the rebuilt total on resume.
3. Since this is a breaking change, any other consumers of LLMLib or CopilotLib outside CodeAssistant must be updated before they upgrade.
4. `CopilotService`'s default model string "claude-sonnet5.5" looks like a typo of "claude-sonnet-5.5". It is unrelated and left unchanged.
