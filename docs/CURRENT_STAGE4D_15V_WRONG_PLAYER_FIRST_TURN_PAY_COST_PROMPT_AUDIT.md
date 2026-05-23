# Stage 4D-15V Wrong Player First Turn Pay Cost Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `PAY_COST` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `PAY_COST` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnPayCostPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `PAY_COST`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `PAY_COST`.
- Accepted first-turn state has no `PendingPayment`.
- Non-active-player `PAY_COST` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `PAY_COST` carrying only the same-tick snapshot reaches the no-payment-window guard and rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `当前没有服务端支付窗口可处理 PAY_COST。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, null `PendingPayment`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, `MOVE_UNIT`, `DECLARE_BATTLE`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `TAP_RUNE`, `RECYCLE_RUNE`, `HIDE_CARD`, `REVEAL_CARD`, `LEGEND_ACT` and `ASSEMBLE_EQUIPMENT`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove command-specific payment-window gating without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
