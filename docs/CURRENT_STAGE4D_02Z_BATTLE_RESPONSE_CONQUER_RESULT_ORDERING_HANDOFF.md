# Stage 4D-02Z Battle Response Conquer Result Ordering Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02Y 已验收 active battle response 中 Shadow stale-target no-effect 会回到 response priority，且 final close 不提前推进 `BF-NEXT`。下一步不继续扩同一 Shadow stale / source breadth 轴，而转向 P0-004 剩余 battle-result ordering matrix。

4D-02Z 锁定一个窄的 conquer result representative：

- natural battle 先打开 battle response priority；
- 双方 pass response 后进入 `ASSIGN_COMBAT_DAMAGE`；
- 合法 assignment 让带 Hunt 的攻击方征服当前 battlefield；
- `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` 必须先于 `BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 与下一处 contested battlefield 推进；
- response window、assignment window 和 result ordering 期间均不得提前推进 `BF-NEXT`。

This is a result-ordering guard, not another Shadow activation breadth slice.

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
NaturalBattleResponsePassAssignmentConquerResultOrdersBeforeNextAdvancement
```

Representative scenario:

1. Start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId])`.
2. Modify `AttackerObjectId` in the test fixture to be a surviving Hunt attacker, for example card no `UNL-059/219` with `CardObjectTags.UnitCard` and `狩猎2`.
3. P1 declares battle at `BF-DAMAGE` with `COMBAT_ASSIGNMENT`.
4. Assert battle response priority opens for P2 and `BF-NEXT` has not advanced.
5. P2 passes response priority; assert `BF-NEXT` has not advanced.
6. P1 passes response priority; assert:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED` precedes `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
   - assignment prompt is open for P1;
   - `BF-NEXT` has not advanced.
7. P1 assigns combat damage using the existing Shadow-response legal assignment shape so both defenders are destroyed while the Hunt attacker survives.
8. Assert assignment result event ordering:
   - `BATTLEFIELD_CONQUERED` for `BattlefieldObjectId`;
   - `EXPERIENCE_GAINED` for P1 and the Hunt source;
   - `DAMAGE_REMOVED` cleanup for the surviving attacker;
   - `BATTLE_CLOSED`;
   - `BATTLEFIELD_CONTROL_RESOLVED`;
   - `BATTLEFIELD_CONTESTED` for `NextBattlefieldObjectId`;
   - `SPELL_DUEL_STARTED` for `NextBattlefieldObjectId`.
9. Assert no `BATTLEFIELD_HELD` appears in this conquer branch, the current `START_BATTLE` task is gone, and the next active task is `task:start-spell-duel:BF-NEXT`.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponsePassAssignmentConquerResultOrdersBeforeNextAdvancement"
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

This is expected to be test-only. Runtime changes are allowed only if the battle response pass -> assignment -> conquer result branch orders result / cleanup / close / control / next-task events incorrectly or advances `BF-NEXT` early.

Passing direct `DECLARE_BATTLE` conquer tests is not enough; 4D-02Z must prove conquer result ordering after a real natural battle response window.
