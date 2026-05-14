# Stage 4D-02Z Battle Response Conquer Result Ordering Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02Z 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 natural battle response pass 后进入 assignment 的 Hunt conquer result ordering：

- natural battle 先打开 battle response priority。
- P2 / P1 pass response 后进入 `ASSIGN_COMBAT_DAMAGE`。
- 合法 assignment 让带 Hunt 的 P1 attacker 摧毁所有 defenders 并存活。
- `BATTLEFIELD_CONQUERED` 与 `EXPERIENCE_GAINED` 在 cleanup / battle close / battlefield control / next contested battlefield advancement 前出现。
- response priority 与 assignment window 期间 `BF-NEXT` 不提前推进。

## Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

This slice exposed a real runtime gap in `ResolveAssignCombatDamage`.

The direct `DECLARE_BATTLE` combat path already emitted Hunt conquest result events, but the assignment prompt path could close the battle, resolve battlefield control, and advance the next battlefield without emitting the matching `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` result for a surviving Hunt attacker.

The accepted fix adds the narrow Hunt conquer result handling to assignment resolution before battle cleanup / close / control resolution:

- detects surviving attacker units after simultaneous assignment damage and state-based cleanup;
- confirms all defenders were destroyed by the assignment cleanup;
- emits `BATTLEFIELD_CONQUERED` with Hunt source metadata;
- applies `GainExperience` and persists updated `PlayerExperience`;
- preserves the existing cleanup / battle close / battlefield control / next task order after result events.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, or full conquer-trigger breadth.

## Behavior Accepted

New guard:

```text
NaturalBattleResponsePassAssignmentConquerResultOrdersBeforeNextAdvancement
```

The test proves direct-conquer result semantics now compose with a real natural battle response window and assignment prompt lifecycle.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02Z_BATTLE_RESPONSE_CONQUER_RESULT_ORDERING_EVIDENCE.md`

Results:

- targeted new guard: 1/1
- focused: 281/281
- adjacent: 811/811
- backend full: 4223/4223
- `git diff --check`: no output

## Residual Risk

This slice narrows battle-result ordering, but it does not close full official P0-004:

- only the response-pass assignment Hunt conquer branch is covered;
- held result ordering in assignment prompt flow remains representative / incomplete;
- full conquer-trigger family beyond Hunt experience is not matrix-complete in assignment flow;
- activation-returned, payment-blocked, cleanup-blocked, no-result and replacement/prevention combinations remain representative, not exhaustive;
- damage modification / prevention / LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
