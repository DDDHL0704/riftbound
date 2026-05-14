# Stage 4D-02AE Battle Response Activation Held-Score Temporary Payment Resource Context Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02AD 已验收 actual Shadow activation / stack resolution / returned response 后 held-score `RECYCLE_RUNE:*` payment-resource context preservation，并补了 no-response 时不执行不必要 recycle 的边界。4D-02AE 转向对称的 temporary payment resource representative，避免 activation-returned held-score payment context 只覆盖 recycle rune。

4D-02AE 锁定一个窄的 activation-returned held-score temporary payment-resource representative：

- natural battle 先打开 battle response priority；
- declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `TEMP_PAYMENT_RESOURCE:<HeldScoreTemporaryResourceId>`；
- P2 从该 response window 真实 activation Shadow，进入 stack；
- stack pass-pass 解析后回到 battle response priority；
- 双方 pass response 后恢复原始 declaration context；
- held-score branch 消费同一个 temporary payment-resource action；
- `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED` / `COST_PAID` / `SCORE_GAINED` audit 必须保留原始 action / temporary resource id；
- no-response 边界不得卡住 response window，也不得无谓消耗 temporary resource。

This composes existing 4D-02J temporary payment-resource preservation with 4D-02K actual activation / stack lifecycle and the 4D-02AD no-response normalization lesson.

## Owner

B 服务端规则 / 协议 / 测试实现：Raman `019e2257-8d40-7630-9201-28df44dd689a`

## Write Scope

Primary expected write:

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

Allowed only if the new guard exposes a real runtime bug:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

No-go:

- Do not modify frontend.
- Do not broaden PaymentEngine / LayerEngine.
- Do not update card coverage matrix.
- Do not touch `riftbound-dotnet.sln`.
- Do not close P0-004, P0-005, P1, READY, or the active goal.

## Required Guards

Add focused server coverage, proposed primary name:

```text
NaturalBattleResponseActivationPreservesHeldScoreTemporaryPaymentResourceContextAfterStackResolution
```

Also add a no-response boundary guard if current runtime would otherwise consume an unnecessary temporary resource:

```text
NaturalBattleResponseDropsUnnecessaryHeldScoreTemporaryResourceContextWhenNoResponseConsumesResources
```

Representative activation scenario:

1. Use `BuildHeldScoreTemporaryPaymentResourceNaturalStartBattleState()` as the base state.
2. Adjust P2 resources for the activation cross-product: P2 should start with `new RunePool(1, 4)` so Shadow can spend 1 mana / 1 power and the final held-score branch can still pay 4 power using the temporary resource.
3. Set:
   - `var temporaryAction = PaymentCostRules.TemporaryPaymentResourceActionId(HeldScoreTemporaryResourceId)`;
   - `var optionalCosts = new[] { "COMBAT_ASSIGNMENT", temporaryAction }`.
4. P1 declares battle at `BattlefieldObjectId`, attackers `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId]`, optional costs `optionalCosts`.
   - Shadow must remain on the battlefield as a legal response source, but should not be in `DefenderObjectIds`.
5. Assert initial `BATTLE_DECLARED` and `BATTLE_RESPONSE_PRIORITY_OPENED` preserve `optionalCosts`.
6. Assert no `BATTLEFIELD_HELD`, `TEMPORARY_PAYMENT_RESOURCE_SPENT`, `COST_PAID`, `SCORE_GAINED`, or `BATTLE_CLOSED` occurs before response resolution.
7. Assert internal declaration context exists in authoritative state but does not leak to P1 / P2 / spectator snapshots or P2 prompt JSON.
8. P2 activates Shadow targeting `AttackerObjectId`; assert `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID`, `STACK_ITEM_ADDED`, stack item present, and internal context still filtered from public projections.
9. P2 / P1 pass stack; assert Shadow resolves, attacker is stunned, battle remains active, priority returns to P2 response, and declaration context still exists without public leakage.
10. P2 / P1 pass returned response priority.
11. Assert final branch:
    - `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts == optionalCosts`;
    - resumed `BATTLE_DECLARED.optionalCosts == optionalCosts`;
    - `BATTLEFIELD_HELD` for the held-score battlefield occurs before held-score trigger / payment / score;
    - `TEMPORARY_PAYMENT_RESOURCE_SPENT.temporaryPaymentResourceId == HeldScoreTemporaryResourceId`;
    - `TEMPORARY_PAYMENT_RESOURCE_CLEARED.temporaryPaymentResourceId == HeldScoreTemporaryResourceId`;
    - `BATTLEFIELD_TRIGGER_RESOLVED.trigger == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `COST_PAID.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `COST_PAID.paymentResourceActions == [temporaryAction]`;
    - `COST_PAID.temporaryPaymentResourceIds == [HeldScoreTemporaryResourceId]`;
    - `COST_PAID.temporaryPaymentResourcePower == 1`;
    - `SCORE_GAINED.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `BATTLE_CLOSED` occurs after the held-score audit events.
12. Assert final state:
    - P2 gained 1 score;
    - P2 power is 0 after payment;
    - `TemporaryPaymentResources` is empty after consumed temporary resource;
    - internal declaration context is cleared;
    - battle is inactive;
    - no stale assignment or battle declaration prompt remains.

No-response boundary scenario:

- Same base / optional costs / P2 `new RunePool(1, 4)`, but P2 / P1 pass response without activating Shadow.
- Expected behavior: response closes and held-score payment succeeds from current resources without consuming the temporary resource, or otherwise has an explicit safe rejection before the battle-response context can get stuck.
- Preferred guard mirrors 4D-02AD: final resumed optional costs drop the unnecessary temporary action, no `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `CLEARED`, P2 keeps the temporary resource, and no stale battle response context remains.

## Acceptance Commands

Targeted new guards:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationPreservesHeldScoreTemporaryPaymentResourceContextAfterStackResolution|FullyQualifiedName~NaturalBattleResponseDropsUnnecessaryHeldScoreTemporaryResourceContextWhenNoResponseConsumesResources"
```

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Whitespace:

```sh
git diff --check
```

## Acceptance Notes

This may expose the same family of runtime gap as 4D-02AD, but for temporary payment resources. Runtime changes are allowed only if actual activation-returned battle response loses `TEMP_PAYMENT_RESOURCE:*` context, leaks the internal carrier, spends the wrong temporary resource action, opens held-score payment too early, consumes an unnecessary temporary resource in the no-response branch, or leaves stale prompt / context state.

Passing 4D-02J or 4D-02K alone is not enough; 4D-02AE must prove their cross-product after real Shadow activation and stack resolution.
