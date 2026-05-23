2026-05-24 Stage 4D-16G pass-priority first-ready after-final-ready envelope audit accepted.

Scope: server P0/P1 closure slice in `src/Riftbound.Engine/CoreRuleEngine.cs` and `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.

Coverage added:

- `PromptIdOnlyPassPriorityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyPassPriorityWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

Behavior proved:

- After both players submit official decks, P1 accepts the first room `READY` prompt and P2's final `READY` starts official mulligan.
- Submitting `PASS_PRIORITY` with P1's old first-ready prompt as `promptId`-only rejects with `PROMPT_EXPIRED` / `行动窗口已过期，请按最新提示重新提交。`.
- Submitting `PASS_PRIORITY` with P1's old first-ready prompt as `snapshotTick`-only is tick-fresh for the current active mulligan prompt and reaches current priority-window legality, rejecting with `PHASE_NOT_ALLOWED` / `让过优先权只能在优先行动窗口中提交。`.
- Both paths preserve events, state hash, tick, RNG cursor, ready ids, mulligan-completed ids, opening second-action player, hands, main decks, current mulligan prompts, prompt ids and snapshot ticks.

Runtime change:

- `CoreRuleEngine` now rejects every typed `PassPriorityCommand` that does not satisfy `CanPassPriority(...)` with the existing `PHASE_NOT_ALLOWED` / `让过优先权只能在优先行动窗口中提交。` guard instead of allowing non-priority-window submissions to fall through to the placeholder fallback.

Validation:

- Focused: `2/2`.
- Adjacent opening/shape/prompt/hash/GameHub/priority filter: `1006/1006`.
- Backend full: `5939/5939`.

Protocol shape changed: no. Hidden-info leakage found: no. Frontend/browser/formal E2E not run because this was a backend server slice.

Project remains **NOT READY**.
