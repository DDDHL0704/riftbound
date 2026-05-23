2026-05-24 Stage 4D-16H pass-focus first-ready after-final-ready envelope audit accepted.

Scope: test-only server P0/P1 closure slice in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.

Coverage added:

- `PromptIdOnlyPassFocusWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyPassFocusWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

Behavior proved:

- After both players submit official decks, P1 accepts the first room `READY` prompt and P2's final `READY` starts official mulligan.
- Submitting `PASS_FOCUS` with P1's old first-ready prompt as `promptId`-only rejects with `PROMPT_EXPIRED` / `行动窗口已过期，请按最新提示重新提交。`.
- Submitting `PASS_FOCUS` with P1's old first-ready prompt as `snapshotTick`-only is tick-fresh for the current active mulligan prompt and reaches current focus-window legality, rejecting with `PHASE_NOT_ALLOWED` / `让过焦点只能在法术对决焦点窗口中提交。`.
- Both paths preserve events, state hash, tick, RNG cursor, ready ids, mulligan-completed ids, opening second-action player, hands, main decks, current mulligan prompts, prompt ids and snapshot ticks.

Validation:

- Focused: `2/2`.
- Adjacent opening/shape/prompt/hash/GameHub filter: `989/989`.
- Backend full: `5941/5941`.

Runtime changed: no. Protocol shape changed: no. Hidden-info leakage found: no. Frontend/browser/formal E2E not run because this was a backend test-only slice.

Project remains **NOT READY**.
