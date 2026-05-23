# Stage 4D-13X Active Player Opposite Wait Prompt Pass Priority Audit

Status: accepted test-only server P0-005 official first-turn active-player opposite `WAIT` prompt `PASS_PRIORITY` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `PASS_PRIORITY` from the active player while carrying the non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `ActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `AssertActivePlayerFirstTurnPassPriorityWithOppositeWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `PASS_PRIORITY`.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `PASS_PRIORITY`.
- Active-player `PASS_PRIORITY` carrying the non-active player's prompt-scoped or prompt-id-only current `WAIT` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Active-player `PASS_PRIORITY` carrying only that same-tick `snapshotTick` passes freshness, reaches core ordinary-main legality and rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `让过优先权只能在优先行动窗口中提交。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn active-player opposite-wait-prompt `PASS_PRIORITY` boundary: prompt id ownership remains enforced for prompt-scoped and prompt-id-only submissions, while same-tick snapshot-only submissions are allowed to reach the core priority-window legality gate and reject without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
