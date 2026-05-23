# Stage 4D-14V Opposite Player First Turn Tap Rune Wait Prompt Audit

Status: accepted test-only server P0-005 official first-turn non-active-player own `WAIT` prompt `TAP_RUNE` boundary slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It submits `TAP_RUNE` from the non-active player while carrying that same non-active player's current first-turn `WAIT` prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `OppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation`.
  - Added `AssertOppositePlayerFirstTurnTapRuneWaitPromptRejectsWithoutMutation`.

## Assertions

- The active player's current first-turn prompt is actionable `MAIN_ACTION`, includes `END_TURN`, and exposes `TAP_RUNE`.
- The non-active player's own current first-turn `WAIT` prompt exposes only `WAIT` plus global `SURRENDER`, and does not expose `TAP_RUNE`.
- Non-active-player `TAP_RUNE` carrying that player's prompt-scoped, prompt-id-only or snapshot-only current `WAIT` prompt envelope is fresh enough to reach the core current-player open-main legality gate.
- All three paths reject with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `横置符文只能在当前玩家的开放主阶段提交。`
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, null `FocusPlayerId`, empty `PassedPriorityPlayerIds`, prompt snapshot ticks and first-turn prompt shape.

## Outcome

Runtime behavior was not changed. This narrows the non-active-player own current `WAIT` prompt boundary for `TAP_RUNE`: current wait-prompt envelopes are accepted by prompt freshness, but do not authorize rune tapping by the non-active player and reject without mutation at the existing server open-main current-player legality gate.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full tap-rune legality breadth, full payment-resource breadth, full movement / battlefield legality breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
