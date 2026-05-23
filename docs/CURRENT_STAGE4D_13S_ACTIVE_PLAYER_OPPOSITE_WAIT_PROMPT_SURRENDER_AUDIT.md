# Stage 4D-13S Active Player Opposite Wait Prompt Surrender Audit

Status: accepted test-only server P0-005 official first-turn active-player opposite wait-prompt `SURRENDER` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `SURRENDER` from the active player while carrying the non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `ActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation`.
  - Added `AssertActivePlayerFirstTurnSurrenderWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `AssertSnapshotOnlyActivePlayerFirstTurnSurrenderWithOppositeWaitPromptReplayAfterMatchFinishedRejectsWithoutMutation`.

## Assertions

- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`.
- Active-player `SURRENDER` carrying the opposite player's prompt-scoped or prompt-id-only wait-prompt envelope rejects with `ErrorCodes.PromptExpired` and exact stale-window diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Those prompt-id rejections emit no events and preserve first-turn state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and first-turn prompt shape.
- Active-player `SURRENDER` carrying only the opposite player's same-tick `snapshotTick` accepts, finishes the match with the non-active player as winner, and preserves the established first-turn terminal prompt contract.
- Replaying that old snapshot-only envelope after the match finishes rejects with `ErrorCodes.PromptExpired` and exact snapshot-expired diagnostics: `行动快照已过期，请按最新状态重新提交。`
- The replay rejection emits no events and preserves terminal state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, winner, prompt snapshot ticks and match-result prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the prompt-id versus snapshot-only boundary for terminal first-turn surrender: prompt ids remain player/prompt scoped, while same-tick snapshot-only freshness still leaves surrender legality to the submitting player and current state.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
