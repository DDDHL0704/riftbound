# Stage 4D-15K Wrong Player First Turn Pass Focus Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `PASS_FOCUS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `PASS_FOCUS` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnPassFocusPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `PASS_FOCUS`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `PASS_FOCUS`.
- Non-active-player `PASS_FOCUS` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `PASS_FOCUS` carrying only the same-tick snapshot reaches the focus-window legality guard and rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `让过焦点只能在法术对决焦点窗口中提交。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS` and `PASS_PRIORITY`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove command-specific focus-window legality without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
