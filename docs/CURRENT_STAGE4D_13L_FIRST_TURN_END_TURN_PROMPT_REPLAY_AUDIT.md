# Stage 4D-13L First Turn End Turn Prompt Replay Audit

Status: accepted runtime plus server P0-005 official first-turn main-action stale-prompt replay slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It then submits the active player's current first-turn `END_TURN` action using the prompt envelope returned by the previous accepted resolution, advances to the next player's main phase, and replays the old first-turn `END_TURN` prompt envelope.

Code changes:

- `src/Riftbound.Engine/MatchSession.cs`
  - `TryRejectStalePrompt` now accepts either the canonical broad `ResolutionResult.BuildPrompts` prompt or the current ordinary-main narrow prompt emitted by `CoreRuleEngine`.
  - This fixes the runtime mismatch where a valid `CoreRuleEngine` ordinary-main prompt could be rejected as stale before command resolution.
- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `FirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation`.
  - Added `AssertOfficialFirstTurnEndTurnNextPlayerPromptQueueAudit`.

## Assertions

- The first-turn active player's prompt-scoped `END_TURN` command is accepted when submitted with the `promptId` and `snapshotTick` returned by the accepted official first-turn prompt.
- The accepted `END_TURN` advances to the second player's main phase, calls three runes, draws one card, clears rune pools and exposes the second player's main-action prompt.
- Replaying the old first-turn `END_TURN` prompt rejects with `ErrorCodes.PromptExpired`.
- The rejection message remains the exact stale-window diagnostic: `行动窗口已过期，请按最新提示重新提交。`
- The replay emits no events and preserves state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, next-player zones and prompt snapshot ticks.
- The old turn player's prompt no longer exposes stale `END_TURN`, and neither prompt exposes stale `MULLIGAN` actions or candidates.
- `ResolutionResult.BuildPrompts` still preserves broad disabled-candidate shape for `PromptFor` and hub consumers; the freshness guard alone accepts the narrow `CoreRuleEngine` prompt for ordinary main windows.

## Outcome

Runtime behavior was changed narrowly in prompt freshness validation. The session now accepts valid ordinary-main prompts returned by `CoreRuleEngine` while still rejecting genuinely stale prompt ids and stale snapshot ticks. This closes the currently observed official first-turn `END_TURN` prompt acceptance/replay gap after final mulligan advances into the first turn.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
