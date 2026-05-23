# Stage 4D-13H Prompt Id Only Mulligan Command After First Turn Audit

Status: accepted test-only server P0-005 official prompt-id-only consumed-mulligan command stale-prompt-after-first-turn audit slice.

## Scope

This slice adds prompt-id-only `MULLIGAN` command replay coverage to `OfficialOpeningTests`:

- `PromptIdOnlyFinalMulliganCommandAfterFirstTurnRejectsWithoutMutation`
- `PromptIdOnlyFirstMulliganCommandAfterFirstTurnRejectsWithoutMutation`

It covers the official opening path where both players have submitted legal decks, both players are ready, both mulligan prompts have been consumed and the game has advanced into the first turn, then the original prompt owner submits a `MULLIGAN` command with only the old consumed first or final mulligan `promptId` and no `snapshotTick`.

## Assertions

- Reusing only the consumed final mulligan `promptId` on a second-player `MULLIGAN` command after first-turn transition rejects with `ErrorCodes.PromptExpired`.
- Reusing only the consumed first mulligan `promptId` on an active-player `MULLIGAN` command after first-turn transition rejects with `ErrorCodes.PromptExpired`.
- The rejection message remains the exact stale-window diagnostic: `行动窗口已过期，请按最新提示重新提交。`
- Every rejection emits no events and preserves state hash, tick, RNG cursor, ready players, mulligan-completed players and `OpeningSecondActionPlayerId`.
- The accepted first-turn state remains stable for active and second player hands, main decks, called rune objects, turn draw objects and prompt snapshot ticks through the existing final-mulligan first-turn prompt audit.
- No stale `MULLIGAN` action or candidate is exposed after the first turn has started.

## Outcome

Runtime behavior was not changed. Existing prompt-id freshness validation rejects the consumed first/final mulligan prompt id even when the raw `MULLIGAN` payload omits `snapshotTick`, before post-opening phase, hand-selection or command replay legality can mutate state.

This narrows prompt-id-only `MULLIGAN` command reuse of consumed first/final mulligan prompts after first-turn transition, stale prompt rejection and first-turn prompt preservation risk. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, frontend build, Chrome smoke, formal 18-step E2E or READY.
