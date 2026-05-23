# Stage 4D-15D Opposite Player First Turn Order Triggers Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn non-active-player own `WAIT` prompt `ORDER_TRIGGERS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `ORDER_TRIGGERS` from the non-active player while carrying that same non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnOrderTriggersWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `ORDER_TRIGGERS`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `ORDER_TRIGGERS`.
- The trigger queue is empty before and after rejection.
- Non-active-player `ORDER_TRIGGERS` carrying that player's prompt-scoped, prompt-id-only or snapshot-only current `WAIT` prompt envelope is fresh enough to reach the existing order-triggers contract shell.
- All three paths reject with `ErrorCodes.InvalidPayload` and exact diagnostics: `ORDER_TRIGGERS 需要非空且不重复的 orderedTriggerIds。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, empty trigger queue, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the non-active-player own current `WAIT` prompt boundary for `ORDER_TRIGGERS`: current wait-prompt envelopes are accepted by prompt freshness, but do not create or enter any trigger-ordering window and reject without mutation at the existing server payload contract shell.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full trigger-ordering legality breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
