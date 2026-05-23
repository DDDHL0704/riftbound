# Stage 4D-15X Wrong Player First Turn Order Triggers Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `ORDER_TRIGGERS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `ORDER_TRIGGERS` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnOrderTriggersPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `ORDER_TRIGGERS`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `ORDER_TRIGGERS`.
- Accepted first-turn state has an empty `TriggerQueue`.
- Non-active-player `ORDER_TRIGGERS` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `ORDER_TRIGGERS` carrying only the same-tick snapshot reaches the trigger-order payload shell and rejects with `ErrorCodes.InvalidPayload` and exact diagnostics: `ORDER_TRIGGERS 需要非空且不重复的 orderedTriggerIds。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, empty `TriggerQueue`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, `MOVE_UNIT`, `DECLARE_BATTLE`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `TAP_RUNE`, `RECYCLE_RUNE`, `HIDE_CARD`, `REVEAL_CARD`, `LEGEND_ACT`, `ASSEMBLE_EQUIPMENT`, `PAY_COST` and `ASSIGN_COMBAT_DAMAGE`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove command-specific trigger-order payload gating without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full trigger-ordering lifecycle breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
