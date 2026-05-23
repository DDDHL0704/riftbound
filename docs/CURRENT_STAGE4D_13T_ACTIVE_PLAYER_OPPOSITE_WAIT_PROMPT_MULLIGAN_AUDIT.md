# Stage 4D-13T Active Player Opposite Wait Prompt Mulligan Audit

Status: accepted test-only server P0-005 official first-turn active-player opposite wait-prompt `MULLIGAN` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `MULLIGAN` from the active player while carrying the non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `ActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation`.
  - Added `AssertActivePlayerFirstTurnMulliganWithOppositeWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose stale `MULLIGAN`.
- The non-active player's current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`.
- Active-player `MULLIGAN` carrying the opposite player's prompt-scoped or prompt-id-only wait-prompt envelope rejects with `ErrorCodes.PromptExpired` and exact stale-window diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Active-player `MULLIGAN` carrying only the opposite player's same-tick `snapshotTick` passes freshness, reaches core legality, and rejects with `ErrorCodes.PhaseNotAllowed` plus exact diagnostics: `起手调整只能在开局调度阶段提交。`
- All three rejection paths emit no events and preserve first-turn state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the first-turn prompt-id versus snapshot-only boundary for a post-opening `MULLIGAN` command: prompt ids remain player/prompt scoped, while same-tick snapshot-only freshness still leaves post-opening mulligan legality to the core phase guard.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
