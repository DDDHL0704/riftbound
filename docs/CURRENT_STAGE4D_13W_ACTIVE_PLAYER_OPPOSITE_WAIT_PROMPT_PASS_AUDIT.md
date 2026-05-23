# Stage 4D-13W Active Player Opposite Wait Prompt Pass Audit

Status: accepted test-only server P0-005 official first-turn active-player opposite `WAIT` prompt `PASS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `PASS` from the active player while carrying the non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `ActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptReplayAfterAcceptedPassRejectsWithoutMutation`.
  - Added `AssertActivePlayerFirstTurnPassWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `AssertSnapshotOnlyActivePlayerFirstTurnPassWithOppositeWaitPromptReplayAfterAcceptedPassRejectsWithoutMutation`.
  - Added `AssertOfficialFirstTurnGenericPassPromptQueueAudit`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION` and includes `END_TURN`.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `PASS`.
- Active-player `PASS` carrying the non-active player's prompt-scoped or prompt-id-only current `WAIT` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Active-player `PASS` carrying only that same-tick `snapshotTick` accepts the generic ordinary-main pass, increments the tick, records `PassedPriorityPlayerIds = [activePlayerId]`, emits one `TURN_ENDED` event with `playerId` and `turnPlayerId`, keeps the first-turn prompt queue in ordinary-main shape, and removes `PASS` from the active prompt after the accepted pass.
- Replaying the old snapshot-only `PASS` envelope after the accepted pass rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动快照已过期，请按最新状态重新提交。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn active-player opposite-wait-prompt `PASS` boundary: prompt id ownership remains enforced for prompt-scoped and prompt-id-only submissions, while same-tick snapshot-only submissions are allowed to reach and execute the core ordinary-main `PASS` transition before old snapshot replay is rejected without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
