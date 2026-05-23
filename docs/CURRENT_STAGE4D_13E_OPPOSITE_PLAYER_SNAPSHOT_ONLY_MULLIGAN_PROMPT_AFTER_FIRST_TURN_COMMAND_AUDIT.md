# Stage 4D-13E Opposite Player Snapshot Only Mulligan Prompt After First Turn Command Audit

Status: accepted test-only server P0-005 official opposite-player snapshot-only consumed-mulligan stale-prompt-after-first-turn command surface audit slice.

## Scope

This slice adds opposite-player snapshot-only first-turn consumed-mulligan prompt coverage to `OfficialOpeningTests`:

- `SnapshotOnlyWrongPlayerRoomCommandsWithFinalMulliganPromptAfterFirstTurnRejectWithoutMutation`
- `SubmitCommandWithFinalMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation`
- `SnapshotOnlyWrongPlayerRoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation`
- `SubmitCommandWithFirstMulliganSnapshotAfterFirstTurnWrongPlayerRejectsWithoutMutation`

It covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn, then the opposite player submits `snapshotTick` from a consumed first or final mulligan prompt without a `promptId` on room commands and submit commands.

## Assertions

- The active player reusing only the consumed final mulligan snapshot on `READY` and `SUBMIT_DECK` rejects with `ErrorCodes.PromptExpired`.
- The second player reusing only the consumed first mulligan snapshot on `READY` and `SUBMIT_DECK` rejects with `ErrorCodes.PromptExpired`.
- Reusing either consumed snapshot without `promptId` on `PASS_PRIORITY`, `PASS_FOCUS`, `PASS`, `END_TURN`, `SURRENDER`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `LEGEND_ACT`, `HIDE_CARD`, `TAP_RUNE`, `RECYCLE_RUNE`, `REVEAL_CARD`, `MOVE_UNIT`, `ASSEMBLE_EQUIPMENT`, `DECLARE_BATTLE`, `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS` and `CHOOSE_HAND_CARDS` rejects with `ErrorCodes.PromptExpired`.
- The rejection message remains the exact snapshot-expired diagnostic: `行动快照已过期，请按最新状态重新提交。`
- Every rejection emits no events and preserves state hash, tick, RNG cursor, ready players, mulligan-completed players and `OpeningSecondActionPlayerId`.
- The accepted first-turn state remains stable for active and second player hands, main decks, called rune objects, turn draw objects and prompt snapshot ticks through the existing final-mulligan first-turn prompt audit.
- No stale `MULLIGAN` action or candidate is exposed after the first turn has started.

## Outcome

Runtime behavior was not changed. Existing snapshot freshness validation rejects the consumed first/final mulligan snapshot for either current player before room, main-action, payment, battle, stack or hand-choice legality can mutate state.

This narrows opposite-player snapshot-only reuse of consumed first/final mulligan prompts after first-turn transition, command-surface stale snapshot rejection and first-turn prompt preservation risk. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, frontend build, Chrome smoke, formal 18-step E2E or READY.
