# Stage 4D-02AA Battle Response Held Result Ordering Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02Z 已验收 natural battle response pass -> assignment -> Hunt conquer result ordering，并补齐 assignment prompt 分支中的 `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` representative。下一步补对称的 held result ordering representative。

4D-02AA 锁定一个窄的 held result branch：

- natural battle 先打开 battle response priority；
- 双方 pass response 后进入 `ASSIGN_COMBAT_DAMAGE`；
- 合法 assignment 让带 Hunt 的防守方守住当前 battlefield；
- `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` 必须先于 `DAMAGE_REMOVED`、`BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 与下一处 contested battlefield 推进；
- response window、assignment window 和 result ordering 期间均不得提前推进 `BF-NEXT`。

This complements 4D-02Z. It should not broaden full held-trigger / payment / replacement behavior.

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
NaturalBattleResponsePassAssignmentHeldResultOrdersBeforeNextAdvancement
```

Representative scenario:

1. Start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId])`.
2. Keep Shadow on the same battlefield as a legal response source but not as a battle defender.
3. Modify the fixture so:
   - `AttackerObjectId` has 1 power and remains a normal unit;
   - `BulwarkDefenderObjectId` becomes a surviving Hunt defender, for example card no `UNL-059/219`, 2 power, tags `[CardObjectTags.UnitCard, "狩猎2"]`.
4. P1 declares battle at `BF-DAMAGE` with `COMBAT_ASSIGNMENT`.
5. Assert battle response priority opens for P2 and `BF-NEXT` has not advanced.
6. P2 passes response priority; assert `BF-NEXT` has not advanced.
7. P1 passes response priority; assert:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED` precedes `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
   - assignment prompt is open for P1;
   - `BF-NEXT` has not advanced.
8. P1 assigns combat damage so the attacker dies and the Hunt defender survives, for example:
   - `AttackerObjectId -> BulwarkDefenderObjectId` for 1;
   - `BulwarkDefenderObjectId -> AttackerObjectId` for 2.
9. Assert assignment result event ordering:
   - `BATTLEFIELD_HELD` for `BattlefieldObjectId`, `playerId = P2`, `sourceObjectId = AttackerObjectId`, `defenderObjectIds = [BulwarkDefenderObjectId]`, `huntAmount = 2`;
   - `EXPERIENCE_GAINED` for P2 and the Hunt defender source;
   - `DAMAGE_REMOVED` cleanup for the surviving defender;
   - `BATTLE_CLOSED`;
   - `BATTLEFIELD_CONTROL_RESOLVED`;
   - `BATTLEFIELD_CONTESTED` for `NextBattlefieldObjectId`;
   - `SPELL_DUEL_STARTED` for `NextBattlefieldObjectId`.
10. Assert no `BATTLEFIELD_CONQUERED` appears in this held branch, the current `START_BATTLE` task is gone, and the next active task is `task:start-spell-duel:BF-NEXT`.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponsePassAssignmentHeldResultOrdersBeforeNextAdvancement"
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

This is expected to mirror the 4D-02Z runtime shape. Runtime changes are allowed only if the battle response pass -> assignment -> held result branch orders result / cleanup / close / control / next-task events incorrectly or advances `BF-NEXT` early.

Passing direct `DECLARE_BATTLE` held tests or existing held-score context tests is not enough; 4D-02AA must prove ordinary held result ordering after a real natural battle response window and assignment prompt.
