# Stage 4D-16B Wrong Player First Turn Surrender Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `SURRENDER` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `SURRENDER` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnSurrenderPromptRejectsWithoutMutation`.
  - Added `AssertSnapshotOnlyWrongPlayerFirstTurnSurrenderPromptReplayAfterMatchFinishedRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION` and exposes global `SURRENDER`.
- The non-active player's own current first-turn `WAIT` prompt exposes `WAIT` plus global `SURRENDER`.
- Non-active-player `SURRENDER` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `SURRENDER` carrying only the same-tick snapshot is freshness-valid for that player's own `WAIT` prompt tick and accepts through the global surrender path, finishing the match with that player as the surrendered player and the active player as the winner.
- Replaying that same snapshot-only envelope after the match is finished rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动快照已过期，请按最新状态重新提交。`
- The prompt-scoped and prompt-id-only rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and first-turn prompt shape. The replay rejection after match finish preserves the finished-state hash, tick, RNG cursor, ready players, mulligan-completed players and final prompt shape.

## Outcome

Runtime behavior was not changed. This completes the exact active-player `MAIN_ACTION` prompt wrong-player command-envelope surface across the current first-turn command family: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions prove the command-specific guard or global surrender path and its stale replay behavior.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full hand-choice lifecycle breadth, full trigger-ordering lifecycle breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
