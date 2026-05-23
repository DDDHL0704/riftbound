# Stage 4D-13U Opposite Player First Turn Mulligan Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn opposite-player `WAIT` prompt `MULLIGAN` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `MULLIGAN` from the non-active player while carrying that player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnMulliganWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose stale `MULLIGAN`.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`.
- Non-active-player `MULLIGAN` carrying that player's current prompt-scoped, prompt-id-only or snapshot-only wait-prompt envelope is freshness-valid enough to reach core legality, then rejects with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `起手调整只能在开局调度阶段提交。`
- All three rejection paths emit no events and preserve first-turn state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn own-wait-prompt `MULLIGAN` boundary: a current wait prompt remains fresh for its owner, but post-opening mulligan legality is rejected by the core phase guard without state mutation.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
