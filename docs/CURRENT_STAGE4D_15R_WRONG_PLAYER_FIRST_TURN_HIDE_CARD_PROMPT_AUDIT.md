# Stage 4D-15R Wrong Player First Turn Hide Card Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `HIDE_CARD` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `HIDE_CARD` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnHideCardPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `HIDE_CARD`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `HIDE_CARD`.
- Non-active-player `HIDE_CARD` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `HIDE_CARD` carrying only the same-tick snapshot reaches the current-player open-main legality guard and rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `待命埋伏只能在当前玩家的开放主阶段提交。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, `MOVE_UNIT`, `DECLARE_BATTLE`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `TAP_RUNE` and `RECYCLE_RUNE`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove command-specific hide-card current-player legality without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
