# Stage 4D-15T Wrong Player First Turn Legend Act Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `LEGEND_ACT` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `LEGEND_ACT` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnLegendActPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `LEGEND_ACT`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `LEGEND_ACT`.
- Non-active-player `LEGEND_ACT` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `LEGEND_ACT` carrying only the same-tick snapshot reaches the legend-action support gate and rejects with `ErrorCodes.UnsupportedCardBehavior` and exact diagnostics: `当前传奇行动尚未由服务端开放。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, `MOVE_UNIT`, `DECLARE_BATTLE`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `TAP_RUNE`, `RECYCLE_RUNE`, `HIDE_CARD` and `REVEAL_CARD`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove command-specific legend-action support gating without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full legend action lifecycle breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
