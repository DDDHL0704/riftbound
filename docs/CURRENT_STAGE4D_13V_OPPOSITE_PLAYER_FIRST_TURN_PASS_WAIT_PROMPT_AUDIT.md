# Stage 4D-13V Opposite Player First Turn Pass Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn opposite-player `WAIT` prompt `PASS` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `PASS` from the non-active player while carrying that player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnPassWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION` and includes `END_TURN`.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `PASS`.
- Non-active-player `PASS` carrying that player's current prompt-scoped, prompt-id-only or snapshot-only wait-prompt envelope is freshness-valid enough to reach core legality, then rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `让过只能由当前玩家在可让过窗口中提交。`
- All three rejection paths emit no events and preserve first-turn state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn own-wait-prompt `PASS` boundary: a current wait prompt remains fresh for its owner, but non-active-player pass legality is rejected by the core turn/window guard without state mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
