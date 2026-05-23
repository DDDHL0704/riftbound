# Stage 4D-14K Active Player Opposite Wait Prompt Assign Combat Damage Audit

Status: accepted test-only server P0-005 official first-turn active-player opposite `WAIT` prompt `ASSIGN_COMBAT_DAMAGE` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `ASSIGN_COMBAT_DAMAGE` from the active player while carrying the non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `ActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `AssertActivePlayerFirstTurnAssignCombatDamageWithOppositeWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `ASSIGN_COMBAT_DAMAGE`.
- The first-turn state has no active battle.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `ASSIGN_COMBAT_DAMAGE`.
- Active-player `ASSIGN_COMBAT_DAMAGE` carrying the non-active player's prompt-scoped or prompt-id-only current `WAIT` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Active-player `ASSIGN_COMBAT_DAMAGE` carrying only that same-tick `snapshotTick` passes freshness, reaches the P0 contract shell and rejects with `ErrorCodes.InvalidPayload` and exact diagnostics: `ASSIGN_COMBAT_DAMAGE 需要 battleId、battlefieldId 与非空 assignments。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, inactive battle state, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn active-player opposite-wait-prompt `ASSIGN_COMBAT_DAMAGE` boundary: prompt id ownership remains enforced for prompt-scoped and prompt-id-only submissions, while same-tick snapshot-only submissions are allowed to reach the combat-damage contract shell and reject without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full battle / combat-damage official breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
