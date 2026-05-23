# Stage 4D-14E Active Player Opposite Wait Prompt Recycle Rune Audit

Status: accepted test-only server P0-005 official first-turn active-player opposite `WAIT` prompt `RECYCLE_RUNE` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `RECYCLE_RUNE` from the active player while carrying the non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `ActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `AssertActivePlayerFirstTurnRecycleRuneWithOppositeWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `RECYCLE_RUNE` and `END_TURN`.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `RECYCLE_RUNE`.
- Active-player `RECYCLE_RUNE` carrying the non-active player's prompt-scoped or prompt-id-only current `WAIT` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Active-player `RECYCLE_RUNE` carrying only that same-tick `snapshotTick` passes freshness, reaches core rune source legality and rejects with `ErrorCodes.InvalidTarget` and exact diagnostics: `回收符文需要选择己方基地中的正面特性符文。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn active-player opposite-wait-prompt `RECYCLE_RUNE` boundary: prompt id ownership remains enforced for prompt-scoped and prompt-id-only submissions even when the active player's own prompt exposes `RECYCLE_RUNE`, while same-tick snapshot-only submissions are allowed to reach the core rune source gate and reject without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full recycle-rune legality breadth, full resource payment breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
