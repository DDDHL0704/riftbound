# Stage 4D-13M First Turn End Turn Envelope Replay Audit

Status: accepted test-only server P0-005 official first-turn main-action prompt-id-only / snapshot-only replay slice.

## Scope

This slice extends the 4D-13L first-turn `END_TURN` replay audit across the two remaining supported freshness envelopes for the same official opening path. After both players submit legal decks, both players are ready, both mulligan prompts are consumed and the game advances into the first turn, the tests submit the active player's current first-turn `END_TURN` action with either only the current `promptId` or only the current `snapshotTick`. Each accepted command advances to the next player's main phase, then replays the old first-turn envelope.

Code changes:

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`
  - Refactored the existing prompt-scoped first-turn `END_TURN` replay test through `AssertFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation`.
  - Added `PromptIdOnlyFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation`.
  - Added `SnapshotOnlyFirstTurnEndTurnPromptReplayAfterNextPlayerStartsRejectsWithoutMutation`.

## Assertions

- A current first-turn `END_TURN` command carrying only the first-turn `promptId` is accepted and advances to the second player's main phase.
- A current first-turn `END_TURN` command carrying only the first-turn `snapshotTick` is accepted and advances to the second player's main phase.
- Replaying the old prompt-id-only first-turn `END_TURN` envelope rejects with `ErrorCodes.PromptExpired` and exact stale-window diagnostics: `行动窗口已过期，请按最新提示重新提交。`
- Replaying the old snapshot-only first-turn `END_TURN` envelope rejects with `ErrorCodes.PromptExpired` and exact snapshot-expired diagnostics: `行动快照已过期，请按最新状态重新提交。`
- Accepted next-turn state calls three runes, draws one card, keeps pending queues idle and exposes only the second player's current main-action prompt.
- Rejections emit no events and preserve state hash, tick, RNG cursor, ready players, mulligan-completed players, `OpeningSecondActionPlayerId`, next-player zones and prompt snapshot ticks.
- The old turn player's prompt no longer exposes stale `END_TURN`, and neither prompt exposes stale `MULLIGAN` actions or candidates.

## Outcome

Runtime behavior was not changed in 4D-13M. The existing 4D-13L prompt-freshness fix now has explicit official coverage for prompt-scoped, prompt-id-only and snapshot-only first-turn `END_TURN` envelopes. This closes the currently enumerated first-turn `END_TURN` envelope replay gap after final mulligan advances into the first turn.

This does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E or READY.
