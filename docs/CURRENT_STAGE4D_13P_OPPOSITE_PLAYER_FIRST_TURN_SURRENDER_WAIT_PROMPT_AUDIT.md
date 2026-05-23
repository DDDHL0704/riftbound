# Stage 4D-13P Opposite Player First Turn Surrender Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn opposite-player `SURRENDER` wait-prompt terminal replay slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits the non-active player's current `WAIT` prompt `SURRENDER` action with each supported prompt envelope, verifies the match finishes with the active player as winner, then replays the old wait-prompt envelope after the terminal state.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnSurrenderWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation`.
  - Generalized `AssertOfficialFirstTurnSurrenderMatchFinishedPromptQueueAudit` so the expected surrendered player and winner can be asserted for either first-turn player.

## Assertions

- The opposite player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, is non-actionable for ordinary game actions, and can still submit `SURRENDER`.
- Prompt-scoped, prompt-id-only and snapshot-only current wait-prompt `SURRENDER` envelopes accept for the opposite player.
- Accepted surrender finishes the match, declares the active player as winner, emits a single `MATCH_WON` event with `surrenderedPlayerId` equal to the opposite player and `reason = SURRENDER`, preserves first-turn zones and clears actionability to match-result prompts.
- Replaying the old prompt-scoped or prompt-id-only wait-prompt `SURRENDER` envelope after the match is finished rejects with `ErrorCodes.PromptExpired` and exact stale-window diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Replaying the old snapshot-only wait-prompt `SURRENDER` envelope after the match is finished rejects with `ErrorCodes.PromptExpired` and exact snapshot-expired diagnostics: `行动快照已过期，请按最新状态重新提交。`
- Rejections emit no events and preserve terminal state hash, tick, RNG cursor, ready players, mulligan-completed players, winner, `OpeningSecondActionPlayerId`, prompt snapshot ticks and match-result prompt shape.
- Terminal prompts expose only `WAIT`; no stale `SURRENDER`, `END_TURN` or `MULLIGAN` actions remain in prompt JSON.

## Outcome

Runtime behavior was not changed. This narrows the official first-turn terminal command boundary for the non-active player: the global `SURRENDER` action remains usable from a wait prompt, and old terminal envelopes cannot mutate the finished match.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
