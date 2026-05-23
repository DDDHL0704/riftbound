# Stage 4D-14Z Opposite Player First Turn Legend Act Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn non-active-player own `WAIT` prompt `LEGEND_ACT` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `LEGEND_ACT` from the non-active player while carrying that same non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnLegendActWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and does not expose `LEGEND_ACT`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `LEGEND_ACT`.
- Non-active-player `LEGEND_ACT` carrying that player's prompt-scoped, prompt-id-only or snapshot-only current `WAIT` prompt envelope is fresh enough to reach the core legend-action support gate.
- All three paths reject with `ErrorCodes.UnsupportedCardBehavior` and exact diagnostics: `当前传奇行动尚未由服务端开放。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the non-active-player own current `WAIT` prompt boundary for `LEGEND_ACT`: current wait-prompt envelopes are accepted by prompt freshness, but do not authorize legend actions and reject without mutation at the existing server legend-action support gate.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full legend-action legality breadth, full payment-resource breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
