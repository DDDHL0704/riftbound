# Stage 4D-13N Wrong Player First Turn End Turn Prompt Audit

Status: accepted test-only server P0-005 official wrong-player first-turn `END_TURN` prompt/envelope rejection slice.

## Scope

This slice covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn. It then has the second player try to submit `END_TURN` with the first player's current first-turn main-action prompt envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Added `WrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation`.
  - Added `PromptIdOnlyWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation`.
  - Added `SnapshotOnlyWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation`.
  - Added `AssertWrongPlayerFirstTurnEndTurnPromptRejectsWithoutMutation`.

## Assertions

- The wrong player carrying the active player's prompt-scoped first-turn `END_TURN` envelope rejects with `ErrorCodes.PromptExpired` and exact stale-window diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- The wrong player carrying only the active player's first-turn `promptId` rejects with `ErrorCodes.PromptExpired` and the same stale-window diagnostics.
- The wrong player carrying only the active player's current `snapshotTick` passes freshness but rejects in core command legality with `ErrorCodes.PhaseNotAllowed` and exact diagnostics: `结束回合只能由当前玩家在开放主阶段提交。`
- All rejections emit no events and preserve first-turn state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, prompt snapshot ticks and the current first-turn prompt queue.
- The active player's first-turn prompt remains actionable with `END_TURN`, while the waiting player remains non-actionable; no stale `MULLIGAN` actions or candidates reappear.

## Outcome

Runtime behavior was not changed. This narrows the official first-turn `END_TURN` ownership boundary for prompt-scoped, prompt-id-only and snapshot-only envelopes: prompt ownership rejects stamped wrong-player submissions, while snapshot-only submissions still cannot mutate because core turn ownership rejects them.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
