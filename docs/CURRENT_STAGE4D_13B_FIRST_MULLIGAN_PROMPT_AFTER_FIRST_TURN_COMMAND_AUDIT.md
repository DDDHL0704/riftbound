# Stage 4D-13B First Mulligan Prompt After First Turn Command Audit

Status: accepted test-only server P0-005 official first-mulligan stale-prompt-after-first-turn command surface audit slice.

## Scope

This slice adds first-mulligan prompt after first-turn command coverage to `OfficialOpeningTests`:

- `RoomCommandsWithFirstMulliganPromptAfterFirstTurnRejectWithoutMutation`
- `SubmitCommandWithFirstMulliganPromptAfterFirstTurnRejectsWithoutMutation`

It covers the official opening path where both players have submitted legal decks, both players are ready, the active mulligan player consumes the first prompt-scoped mulligan, the second player accepts final mulligan and advances the game into the first turn, then the active player reuses the consumed first mulligan prompt id / snapshot tick on room commands and submit commands.

## Assertions

- Reusing the consumed first mulligan prompt on `READY` and `SUBMIT_DECK` rejects with `ErrorCodes.PromptExpired`.
- Reusing the same consumed prompt on `PASS_PRIORITY`, `PASS_FOCUS`, `PASS`, `END_TURN`, `SURRENDER`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `LEGEND_ACT`, `HIDE_CARD`, `TAP_RUNE`, `RECYCLE_RUNE`, `REVEAL_CARD`, `MOVE_UNIT`, `ASSEMBLE_EQUIPMENT`, `DECLARE_BATTLE`, `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS` and `CHOOSE_HAND_CARDS` rejects with `ErrorCodes.PromptExpired`.
- The rejection message remains the exact stale-window diagnostic: `行动窗口已过期，请按最新提示重新提交。`
- Every rejection emits no events and preserves state hash, tick, RNG cursor, ready players, mulligan-completed players and `OpeningSecondActionPlayerId`.
- The accepted first-turn state remains stable for active and second player hands, main decks, called rune objects, turn draw objects and prompt snapshot ticks through the existing final-mulligan first-turn prompt audit.
- No stale `MULLIGAN` action or candidate is exposed after the first turn has started.

## Outcome

Runtime behavior was not changed. Existing prompt-id validation rejects the consumed first mulligan prompt before room, main-action, payment, battle, stack or hand-choice legality can run.

This narrows official first-mulligan prompt consumption after first-turn transition, command-surface stale prompt rejection and first-turn prompt preservation risk. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, frontend build, Chrome smoke, formal 18-step E2E or READY.
