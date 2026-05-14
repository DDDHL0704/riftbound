# Stage 4D-02AD Battle Response Activation Held-Score Payment Resource Context Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02I 已验收 natural battle response pass-pass 后 `RECYCLE_RUNE:*` held-score payment-resource context preservation。4D-02K 已验收 actual Shadow activation / stack resolution 后 Brush replacement context preservation。4D-02AD 把这两个轴交叉起来，避免 payment-resource optional cost 只在无人实际响应的 response window 中有 guard。

4D-02AD 锁定一个窄的 activation-returned held-score payment-resource representative：

- natural battle 先打开 battle response priority；
- declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `RECYCLE_RUNE:<HeldScoreRecycleRuneObjectId>`；
- P2 从该 response window 真实 activation Shadow，进入 stack；
- stack pass-pass 解析后回到 battle response priority；
- 双方 pass response 后恢复原始 declaration context；
- held-score branch 消费同一个 `RECYCLE_RUNE:*` payment-resource action；
- `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID` / `SCORE_GAINED` audit 必须保留原始 action / rune id；
- internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier 在 activation、stack resolution 和 final branch 期间不泄露到 player / spectator snapshots 或 prompt JSON。

This composes existing 4D-02I payment-resource preservation with 4D-02K actual activation / stack lifecycle.

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

## Required Guard

Add one focused server test, proposed name:

```text
NaturalBattleResponseActivationPreservesHeldScorePaymentResourceContextAfterStackResolution
```

Representative scenario:

1. Use `BuildHeldScorePaymentResourceNaturalStartBattleState()` as the base state.
2. Adjust P2 resources for the activation cross-product: P2 should start with `new RunePool(1, 4)` so Shadow can spend 1 mana / 1 power and the final held-score branch can still pay 4 power using `RECYCLE_RUNE:<HeldScoreRecycleRuneObjectId>`.
3. Set:
   - `var recycleAction = $"RECYCLE_RUNE:{HeldScoreRecycleRuneObjectId}"`;
   - `var optionalCosts = new[] { "COMBAT_ASSIGNMENT", recycleAction }`.
4. P1 declares battle at `BattlefieldObjectId`, attackers `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId]`, optional costs `optionalCosts`.
   - Shadow must remain on the battlefield as a legal response source, but should not be in `DefenderObjectIds`.
5. Assert initial `BATTLE_DECLARED` and `BATTLE_RESPONSE_PRIORITY_OPENED` preserve `optionalCosts`.
6. Assert no `BATTLEFIELD_HELD`, `RUNE_RECYCLED`, `COST_PAID`, `SCORE_GAINED`, or `BATTLE_CLOSED` occurs before response resolution.
7. Assert internal declaration context exists in authoritative state but does not leak to P1 / P2 / spectator snapshots or P2 prompt JSON.
8. P2 activates Shadow targeting `AttackerObjectId`; assert `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID`, `STACK_ITEM_ADDED`, stack item present, and internal context still filtered from public projections.
9. P2 / P1 pass stack; assert Shadow resolves, attacker is stunned, battle remains active, priority returns to P2 response, and declaration context still exists without public leakage.
10. P2 / P1 pass returned response priority.
11. Assert final branch:
    - `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts` equals `optionalCosts`;
    - resumed `BATTLE_DECLARED.optionalCosts` equals `optionalCosts`;
    - `BATTLEFIELD_HELD` for the held-score battlefield occurs before held-score trigger / payment / score;
    - `RUNE_RECYCLED.sourceObjectId == HeldScoreRecycleRuneObjectId`;
    - `POWER_GAINED.sourceObjectId == HeldScoreRecycleRuneObjectId`;
    - `BATTLEFIELD_TRIGGER_RESOLVED.trigger == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `COST_PAID.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `COST_PAID.paymentResourceActions == [recycleAction]`;
    - `COST_PAID.recycledRuneObjectIds == [HeldScoreRecycleRuneObjectId]`;
    - `SCORE_GAINED.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `BATTLE_CLOSED` occurs after the held-score audit events.
12. Assert final state:
    - P2 gained 1 score;
    - P2 power is 0 after payment;
    - `HeldScoreRecycleRuneObjectId` moved from base to rune deck;
    - internal declaration context is cleared;
    - battle is inactive;
    - no stale assignment or battle declaration prompt remains.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationPreservesHeldScorePaymentResourceContextAfterStackResolution"
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

This is expected to be test-only. Runtime changes are allowed only if actual activation-returned battle response loses `RECYCLE_RUNE:*` context, leaks the internal carrier, spends the wrong payment-resource action, opens held-score payment too early, or leaves stale prompt / context state.

Passing 4D-02I or 4D-02K alone is not enough; 4D-02AD must prove their cross-product after real Shadow activation and stack resolution.
