# Stage 4D-02AA Battle Response Held Result Ordering Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02AA 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 natural battle response pass 后进入 assignment 的 Hunt held result ordering：

- natural battle 先打开 battle response priority。
- P2 / P1 pass response 后进入 `ASSIGN_COMBAT_DAMAGE`。
- 合法 assignment 让带 Hunt 的 P2 defender 摧毁 attacker 并守住当前 battlefield。
- `BATTLEFIELD_HELD` 与 `EXPERIENCE_GAINED` 在 cleanup / battle close / battlefield control / next contested battlefield advancement 前出现。
- response priority 与 assignment window 期间 `BF-NEXT` 不提前推进。

## Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

This slice exposed a real runtime gap in `CommitCombatDamageAssignments`.

4D-02Z added Hunt conquer result handling to the assignment prompt path. The symmetric defender-held Hunt path still did not emit `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` before cleanup, battle close, battlefield control resolution, and next battlefield advancement.

The accepted fix adds narrow defender-held Hunt handling to assignment resolution before battle cleanup / close / control resolution:

- confirms the resolved battle winner is the defending player;
- detects surviving defender units after simultaneous assignment damage and state-based cleanup;
- emits `BATTLEFIELD_HELD` with Hunt source metadata;
- applies `GainExperience` and persists updated `PlayerExperience`;
- preserves existing cleanup / battle close / battlefield control / next task ordering after result events.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, held-score payment, replacement, or full held-trigger breadth.

## Behavior Accepted

New guard:

```text
NaturalBattleResponsePassAssignmentHeldResultOrdersBeforeNextAdvancement
```

The test proves direct-held Hunt result semantics now compose with a real natural battle response window and assignment prompt lifecycle.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02AA_BATTLE_RESPONSE_HELD_RESULT_ORDERING_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 282/282
- adjacent: 812/812
- backend full: 4224/4224
- `git diff --check`: no output

## Residual Risk

This slice narrows battle-result ordering, but it does not close full official P0-004:

- only the response-pass assignment Hunt held branch is covered;
- full held-trigger family beyond Hunt experience is not matrix-complete in assignment flow;
- held-score payment, Brush replacement, temporary resource, and trigger interactions remain representative, not exhaustive;
- activation-returned, payment-blocked, cleanup-blocked, no-result and replacement/prevention combinations remain representative;
- damage modification / prevention / LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
