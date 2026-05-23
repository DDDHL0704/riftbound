2026-05-24 Stage 4D-16E duplicate-mulligan first-ready after-final-ready envelope audit accepted.

Scope: test-only server P0/P1 closure slice in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.

Coverage added:

- `PromptIdOnlyDuplicateMulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyDuplicateMulliganWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

Behavior proved:

- After both players submit official decks, P1 accepts the first room `READY` prompt and P2's final `READY` starts official mulligan.
- Submitting a duplicate-selection `MULLIGAN` with P1's old first-ready prompt as `promptId`-only rejects with `PROMPT_EXPIRED` / `行动窗口已过期，请按最新提示重新提交。`.
- Submitting a duplicate-selection `MULLIGAN` with P1's old first-ready prompt as `snapshotTick`-only is tick-fresh for the current active mulligan prompt and reaches the current mulligan payload guard, rejecting with `INVALID_TARGET` / `起手调整不能重复选择同一张牌。`.
- Both paths preserve events, state hash, tick, RNG cursor, ready ids, mulligan-completed ids, opening second-action player, hands, main decks, current mulligan prompts, prompt ids and snapshot ticks.

Validation:

- Focused: `2/2`.
- Adjacent opening/shape/prompt/hash/GameHub filter: `983/983`.
- Backend full: `5935/5935`.

Runtime changed: no. Protocol shape changed: no. Hidden-info leakage found: no. Frontend/browser/formal E2E not run because this was a backend test-only slice.

Project remains **NOT READY**.
