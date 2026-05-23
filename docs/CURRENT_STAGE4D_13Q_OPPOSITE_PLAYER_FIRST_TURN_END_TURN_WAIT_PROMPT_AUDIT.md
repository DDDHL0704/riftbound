# Stage 4D-13Q Opposite Player First Turn End Turn Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn opposite-player `END_TURN` wait-prompt rejection slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `END_TURN` from the non-active player using that player's current first-turn `WAIT` prompt envelope, verifying prompt freshness passes but core turn ownership rejects without mutation.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnEndTurnWaitPromptRejectsWithoutMutation`.

## Assertions

- The opposite player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`; it does not expose `END_TURN`.
- Prompt-scoped, prompt-id-only and snapshot-only current wait-prompt envelopes pass prompt freshness and reject `END_TURN` with `ErrorCodes.PhaseNotAllowed`.
- All three rejections return exact core turn-ownership diagnostics: `结束回合只能由当前玩家在开放主阶段提交。`
- Rejections emit no events and preserve first-turn state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and first-turn prompt shape.
- Current first-turn prompts keep the active player actionable for main action and the opposite player on `WAIT` / `SURRENDER`; no stale `MULLIGAN` action or candidate is exposed.

## Outcome

Runtime behavior was not changed. This narrows the first-turn wait-prompt command boundary: the non-active player's own current prompt is fresh, but it cannot authorize `END_TURN`.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
