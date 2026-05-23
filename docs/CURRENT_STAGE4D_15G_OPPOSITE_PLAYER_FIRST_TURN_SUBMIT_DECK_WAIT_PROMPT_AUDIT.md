# Stage 4D-15G Opposite Player First Turn Submit Deck Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn non-active-player own `WAIT` prompt `SUBMIT_DECK` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `SUBMIT_DECK` from the non-active player while carrying that same non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnSubmitDeckWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `READY` or `SUBMIT_DECK`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `READY` or `SUBMIT_DECK`.
- Non-active-player `SUBMIT_DECK` carrying that player's prompt-scoped, prompt-id-only or snapshot-only current `WAIT` prompt envelope is fresh enough to reach the already-in-progress submit-deck guard.
- All three paths reject with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `对局开始后不能更改卡组。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the non-active-player own current `WAIT` prompt boundary for `SUBMIT_DECK`: current wait-prompt envelopes are accepted by prompt freshness, but do not reopen deck submission after match start and reject without mutation at the existing in-progress submit-deck guard.

This completes the current non-active-player own first-turn `WAIT` prompt command lane across the current `CommandTypes` surface, but does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full battle / assignment legality breadth, full payment-resource breadth, full pending-payment official breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
