# Stage 4D-02AB Battle Response Activation Conquer Result Ordering Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02Z / 4D-02AA 已验收 response-pass assignment 中 Hunt conquer / held result ordering。下一步把 result ordering 接到 actual activation-returned assignment branch，避免只证明 pass-only response window。

4D-02AB 锁定一个窄的 activation-returned conquer representative：

- natural battle 先打开 battle response priority；
- P2 从该 response window 真实 activation Shadow，进入 stack；
- stack pass-pass 解析后回到 battle response priority；
- 双方 pass response 后进入 `ASSIGN_COMBAT_DAMAGE`；
- 合法 assignment 让带 Hunt 的攻击方征服当前 battlefield；
- `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` 必须先于 `DAMAGE_REMOVED`、`BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 与下一处 contested battlefield 推进；
- stack-open、stack-resolved returned response、assignment window 和 result ordering 期间均不得提前推进 `BF-NEXT`。

This should compose existing 4D-02O activation-returned assignment advancement with 4D-02Z conquer result ordering.

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
NaturalBattleResponseActivationAssignmentConquerResultOrdersBeforeNextAdvancement
```

Representative scenario:

1. Start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId])`.
2. Modify `AttackerObjectId` to be a surviving Hunt attacker, for example card no `UNL-059/219`, 5 power, tags `[CardObjectTags.UnitCard, "狩猎2"]`.
3. P1 declares battle at `BF-DAMAGE` with `COMBAT_ASSIGNMENT`.
4. Assert battle response priority opens for P2 and `BF-NEXT` has not advanced.
5. P2 activates Shadow targeting `AttackerObjectId`; assert stack item is added and `BF-NEXT` has not advanced.
6. P2 / P1 pass stack priority; assert Shadow resolves, attacker is stunned, battle remains active, priority returns to P2 response, and `BF-NEXT` has not advanced.
7. P2 / P1 pass returned response priority; assert:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED` precedes `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
   - assignment prompt is open for P1;
   - `BF-NEXT` has not advanced.
8. P1 assigns combat damage using the existing Shadow-response legal assignment shape so both defenders are destroyed while the Hunt attacker survives.
9. Assert assignment result event ordering:
   - `BATTLEFIELD_CONQUERED` for `BattlefieldObjectId`;
   - `EXPERIENCE_GAINED` for P1 and the Hunt attacker source;
   - `DAMAGE_REMOVED` cleanup for the surviving attacker;
   - `BATTLE_CLOSED`;
   - `BATTLEFIELD_CONTROL_RESOLVED`;
   - `BATTLEFIELD_CONTESTED` for `NextBattlefieldObjectId`;
   - `SPELL_DUEL_STARTED` for `NextBattlefieldObjectId`.
10. Assert no `BATTLEFIELD_HELD` appears in this conquer branch, the current `START_BATTLE` task is gone, and the next active task is `task:start-spell-duel:BF-NEXT`.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationAssignmentConquerResultOrdersBeforeNextAdvancement"
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

This is expected to be test-only after 4D-02Z. Runtime changes are allowed only if actual activation-returned assignment loses result context, orders result / cleanup / close / control / next-task events incorrectly, or advances `BF-NEXT` early.

Passing 4D-02O or 4D-02Z alone is not enough; 4D-02AB must prove their cross-product after real Shadow activation and stack resolution.
