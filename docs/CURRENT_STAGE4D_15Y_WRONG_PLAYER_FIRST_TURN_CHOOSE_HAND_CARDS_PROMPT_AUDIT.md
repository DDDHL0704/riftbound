# Stage 4D-15Y Wrong Player First Turn Choose Hand Cards Prompt Audit

Status: accepted test-only server P0-005 official first-turn wrong-player current `MAIN_ACTION` prompt `CHOOSE_HAND_CARDS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `CHOOSE_HAND_CARDS` from the non-active player while carrying the active player's current first-turn `MAIN_ACTION` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnChooseHandCardsPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `CHOOSE_HAND_CARDS`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `CHOOSE_HAND_CARDS`.
- Accepted first-turn state has no `PendingHandChoice`.
- Non-active-player `CHOOSE_HAND_CARDS` carrying the active player's prompt-scoped or prompt-id-only current `MAIN_ACTION` prompt envelope rejects with `ErrorCodes.PromptExpired` and exact diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Non-active-player `CHOOSE_HAND_CARDS` carrying only the same-tick snapshot reaches the no-hand-choice-window guard and rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `当前没有服务端手牌选择窗口可处理 CHOOSE_HAND_CARDS。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, null `PendingHandChoice`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This continues the current active-player `MAIN_ACTION` prompt wrong-player boundary beyond `END_TURN`, `MULLIGAN`, `PASS`, `PASS_PRIORITY`, `PASS_FOCUS`, `MOVE_UNIT`, `DECLARE_BATTLE`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `TAP_RUNE`, `RECYCLE_RUNE`, `HIDE_CARD`, `REVEAL_CARD`, `LEGEND_ACT`, `ASSEMBLE_EQUIPMENT`, `PAY_COST`, `ASSIGN_COMBAT_DAMAGE` and `ORDER_TRIGGERS`: prompt id ownership is rejected before command handling, while snapshot-only same-tick submissions still prove command-specific hand-choice-window gating without mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full hand-choice lifecycle breadth, full trigger-ordering lifecycle breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
