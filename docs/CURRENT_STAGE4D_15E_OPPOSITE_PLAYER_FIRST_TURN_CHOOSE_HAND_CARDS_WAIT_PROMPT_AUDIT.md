# Stage 4D-15E Opposite Player First Turn Choose Hand Cards Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn non-active-player own `WAIT` prompt `CHOOSE_HAND_CARDS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `CHOOSE_HAND_CARDS` from the non-active player while carrying that same non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnChooseHandCardsWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `CHOOSE_HAND_CARDS`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `CHOOSE_HAND_CARDS`.
- There is no `PendingHandChoice` before or after rejection.
- Non-active-player `CHOOSE_HAND_CARDS` carrying that player's prompt-scoped, prompt-id-only or snapshot-only current `WAIT` prompt envelope is fresh enough to reach the existing hand-choice contract shell.
- All three paths reject with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `当前没有服务端手牌选择窗口可处理 CHOOSE_HAND_CARDS。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, null `PendingHandChoice`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the non-active-player own current `WAIT` prompt boundary for `CHOOSE_HAND_CARDS`: current wait-prompt envelopes are accepted by prompt freshness, but do not create or enter any hand-choice window and reject without mutation at the existing server hand-choice contract shell.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full hand-choice legality breadth, full trigger-ordering legality breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
