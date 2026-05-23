2026-05-24 Stage 4D-16D submit-deck first-ready after-final-ready envelope audit accepted.

Scope: test-only server P0/P1 closure slice in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.

Coverage added:

- `PromptIdOnlySubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlySubmitDeckWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

Behavior proved:

- After both players submit official decks, P1 accepts the first room `READY` prompt and P2's final `READY` starts official mulligan.
- Replaying P1's old first-ready prompt on `SUBMIT_DECK` as `promptId`-only rejects with `PROMPT_EXPIRED` / `行动窗口已过期，请按最新提示重新提交。`.
- Replaying P1's old first-ready prompt on `SUBMIT_DECK` as `snapshotTick`-only is tick-fresh for P1's current prompt but reaches the in-progress deck-change guard and rejects with `PHASE_NOT_ALLOWED` / `对局开始后不能更改卡组。`.
- Both paths preserve events, state hash, tick, RNG cursor, ready ids, mulligan-completed ids, opening second-action player, hands, main decks, submitted decklists, current mulligan prompts, prompt ids and snapshot ticks.

Validation:

- Focused: `2/2`.
- Adjacent opening/shape/prompt/hash/GameHub filter: `981/981`.
- Backend full: `5933/5933`.

Runtime changed: no. Protocol shape changed: no. Hidden-info leakage found: no. Frontend/browser/formal E2E not run because this was a backend test-only slice.

Project remains **NOT READY**.
