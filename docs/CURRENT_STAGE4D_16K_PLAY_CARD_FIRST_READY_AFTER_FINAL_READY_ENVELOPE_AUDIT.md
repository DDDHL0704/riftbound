2026-05-24 Stage 4D-16K play-card first-ready after-final-ready envelope audit accepted.

Scope: test-only server P0/P1 closure slice in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.

Coverage added:

- `PromptIdOnlyPlayCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyPlayCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

Behavior proved:

- After both players submit official decks, P1 accepts the first room `READY` prompt and P2's final `READY` starts official mulligan.
- Submitting `PLAY_CARD` with P1's old first-ready prompt as `promptId`-only rejects with `PROMPT_EXPIRED` / `行动窗口已过期，请按最新提示重新提交。`.
- Submitting `PLAY_CARD` with P1's old first-ready prompt as `snapshotTick`-only is tick-fresh for the current active mulligan prompt and reaches current card behavior lookup, rejecting with `UNSUPPORTED_CARD_BEHAVIOR` / `Unsupported card behavior or mode: MISSING-CARD `.
- Both paths preserve events, state hash, tick, RNG cursor, ready ids, mulligan-completed ids, opening second-action player, hands, main decks, current mulligan prompts, prompt ids and snapshot ticks.

Validation:

- Focused: `2/2`.
- Adjacent opening/shape/prompt/hash/GameHub filter: `995/995`.
- Backend full: `5947/5947`.

Runtime changed: no. Protocol shape changed: no. Hidden-info leakage found: no. Frontend/browser/formal E2E not run because this was a backend test-only slice.

Project remains **NOT READY**.
