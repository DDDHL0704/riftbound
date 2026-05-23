# Stage 4D-16A Wrong Player First Turn Submit Deck Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `SUBMIT_DECK` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `SUBMIT_DECK` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnSubmitDeckPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `READY` or `SUBMIT_DECK`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `READY` or `SUBMIT_DECK`.
- Non-active-player `SUBMIT_DECK` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `SUBMIT_DECK` carrying only the same-tick snapshot is freshness-valid for that player's own `WAIT` prompt tick, reaches the in-progress deck-change guard, and rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `对局开始后不能更改卡组。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, `MOVE_UNIT`, `DECLARE_BATTLE`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `TAP_RUNE`, `RECYCLE_RUNE`, `HIDE_CARD`, `REVEAL_CARD`, `LEGEND_ACT`, `ASSEMBLE_EQUIPMENT`, `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS`, `CHOOSE_HAND_CARDS` and `READY`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove the command-specific in-progress submit-deck guard without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full hand-choice lifecycle breadth, full trigger-ordering lifecycle breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
