# Stage 4D-02AE Battle Response Activation Held-Score Temporary Payment Resource Context Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

4D-02AE 接受一个 P0-004 battle lifecycle focused guard，用于覆盖 actual Shadow activation / stack resolution / returned response 后的 held-score `TEMP_PAYMENT_RESOURCE:*` payment-resource context preservation：

- natural battle 先打开 battle response priority；
- declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `TEMP_PAYMENT_RESOURCE:<HeldScoreTemporaryResourceId>`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- Shadow 消耗资源后，returned response priority 仍保留 declaration context；
- final response pass 恢复原始 optional costs；
- held-score branch 消费同一个 `TEMP_PAYMENT_RESOURCE:*` action，并输出 `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED` / `COST_PAID` / `SCORE_GAINED` audit；
- no-response 边界会在 final resume 前剔除已不必要的 temporary payment-resource action，避免 response window 卡住或无谓消耗 temporary resource。

## Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Runtime Change

This slice exposed the temporary-payment-resource half of the same runtime gap that 4D-02AD fixed for `RECYCLE_RUNE:*`.

Before 4D-02AE, `TEMP_PAYMENT_RESOURCE:*` optional cost had to be necessary at initial `DECLARE_BATTLE` validation time. That rejected a legitimate activation-returned battle-response path: P2 initially had enough power, but Shadow response activation spent power before the final held-score branch, making the temporary resource necessary at resume time.

The accepted fix is deliberately narrow:

- initial active battle-response context capture can defer the temporary resource necessity check only while opening a battle-response window from an active `START_BATTLE` task;
- final resume still revalidates held-score payment-resource costs;
- if no response consumes resources and the temporary resource action is still unnecessary at resume time, `NormalizeBattleResponseResumeDeclareBattleCommand` removes the held-score payment-resource action before resolving the battle;
- final held-score payment validation remains strict when a temporary resource action is still present.

This does not broaden PaymentEngine, LayerEngine, frontend, card matrix, replacement, prevention, or full combat breadth.

## Behavior Accepted

New guards:

```text
NaturalBattleResponseActivationPreservesHeldScoreTemporaryPaymentResourceContextAfterStackResolution
NaturalBattleResponseDropsUnnecessaryHeldScoreTemporaryResourceContextWhenNoResponseConsumesResources
```

The tests prove:

- `TEMP_PAYMENT_RESOURCE:*` optional cost survives actual Shadow activation, stack resolution, returned response priority, and final response pass when the response consumes resources that make it necessary;
- the final held-score branch consumes the original temporary resource action and preserves the submitted action / temporary resource id in audit events;
- internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` remains filtered from player / spectator snapshots and prompt JSON;
- if no response consumes resources, the unnecessary temporary resource action is dropped on resume, no temporary resource is spent or cleared, held-score payment succeeds from current resources, and the response window does not get stuck.

## Validation

Evidence is recorded in:

- `docs/CURRENT_STAGE4D_02AE_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_TEMP_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`

Results:

- targeted new / boundary guards: 2/2
- focused: 288/288
- adjacent: 818/818
- backend full: 4230/4230
- `git diff --check`: no output

## Residual Risk

This slice narrows held-score payment-resource context preservation, but it does not close full official P0-004:

- only activation-returned `TEMP_PAYMENT_RESOURCE:*` held-score payment-resource context is paired here with the already accepted `RECYCLE_RUNE:*` representative;
- full held / conquer trigger, replacement, prevention, damage-modification, cleanup-blocker, and payment-interaction matrices remain open;
- arbitrary official multi-combat assignment and all same-priority permutations remain outside this focused guard;
- LayerEngine interactions remain deferred;
- final frontend, Chrome smoke, formal E2E, hidden-info long-chain, card coverage matrix, and completion audit remain open.

Project remains **NOT READY**.
