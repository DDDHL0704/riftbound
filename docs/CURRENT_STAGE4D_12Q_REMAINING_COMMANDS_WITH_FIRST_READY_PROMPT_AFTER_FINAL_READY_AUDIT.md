# Stage 4D-12Q Remaining Commands With First Ready Prompt After Final Ready Audit

Status: accepted test-only server P0-005 official remaining-command set with first READY stale-prompt-after-final-READY / mulligan prompt preservation audit slice.

## Scope

This slice adds the remaining official command tests to `OfficialOpeningTests`:

- `LegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `AssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `AssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `OrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `ChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

It covers the official opening path where both players have submitted legal decks, P1 accepts the first prompt-scoped `READY`, P2 accepts final `READY` and advances the game into official mulligan, and then the active mulligan player carries P1's original first READY prompt id / snapshot tick on each remaining command type.

## Assertions

- P1's first prompt-scoped `READY` is accepted and leaves the room in the both-decks single-ready state.
- P2's final `READY` advances to `IN_PROGRESS` / `MULLIGAN`, emits `PLAYER_READY`, `OFFICIAL_OPENING_STARTED` and `MATCH_STARTED`, and exposes only mulligan prompts.
- The active mulligan player carrying P1's original first READY prompt id / snapshot tick on `LEGEND_ACT`, `ASSEMBLE_EQUIPMENT`, `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS` and `CHOOSE_HAND_CARDS` after final READY rejects with `ErrorCodes.PromptExpired`.
- The rejection message remains the exact stale-window diagnostic: `行动窗口已过期，请按最新提示重新提交。`
- Each rejection emits no events and preserves state hash, tick, RNG cursor, ready players, both players' hands, both players' main decks, mulligan-completed players and `OpeningSecondActionPlayerId`.
- The current mulligan prompt queue remains stable, including active and second-player prompt ids / snapshot ticks.
- No stale room `READY` / `SUBMIT_DECK` action or candidate is exposed after final READY or after the cross-command stale prompt rejection.

## Outcome

Runtime behavior was not changed. Existing prompt-id validation already rejects the stale room prompt before each remaining command's source / target / payment / task legality, and existing mulligan prompt builders preserve the current prompt queue.

This closes the currently enumerated `CommandTypes` first READY stale-prompt-after-final-READY command surface for the official opening stale-room-prompt family. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, frontend build, Chrome smoke, formal 18-step E2E or READY.
