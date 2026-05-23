# Stage 4D-15C Opposite Player First Turn Assign Combat Damage Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn non-active-player own `WAIT` prompt `ASSIGN_COMBAT_DAMAGE` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `ASSIGN_COMBAT_DAMAGE` from the non-active player while carrying that same non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnAssignCombatDamageWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `ASSIGN_COMBAT_DAMAGE`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `ASSIGN_COMBAT_DAMAGE`.
- There is no active battle before or after rejection.
- Non-active-player `ASSIGN_COMBAT_DAMAGE` carrying that player's prompt-scoped, prompt-id-only or snapshot-only current `WAIT` prompt envelope is fresh enough to reach the existing assign-combat-damage contract shell.
- All three paths reject with `ErrorCodes.InvalidPayload` and exact diagnostics: `ASSIGN_COMBAT_DAMAGE 需要 battleId、battlefieldId 与非空 assignments。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, inactive battle state, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the non-active-player own current `WAIT` prompt boundary for `ASSIGN_COMBAT_DAMAGE`: current wait-prompt envelopes are accepted by prompt freshness, but do not create or enter any battle damage assignment window and reject without mutation at the existing server payload contract shell.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
