# Stage 4D-02X Battle Response Multiple Legal Sources Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02W 已验收 active battle response window 中的 nested standby reaction stack representative。下一步继续收窄 P0-004 battle-response breadth，但换到新的矩阵轴：同一个 battle response window 中存在多个独立合法 response sources，且它们可以在同一响应窗口内先后各自创建 stack item。

本切片目标不是再扩 standby reaction，也不是扩 PaymentEngine / LayerEngine，而是证明服务端 prompt / command / stack-return 语义能在一个 battle response window 中维护多个 source 的可用性：

- 初始 response prompt 同时公开两个 ready Shadow sources。
- 第一个 Shadow activation / stack resolution 后，已 exhausted source 不再公开，第二个仍可作为合法 response source。
- 第二个 Shadow activation / stack resolution 后，battle response priority 仍正确返回。
- `BF-NEXT` 在两个 response stack item 和中间 priority pass 期间均不得提前推进；只能在最终 response close / battle close / control resolve 后推进。

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
- Do not modify PaymentEngine / LayerEngine breadth.
- Do not update card coverage matrix.
- Do not touch `riftbound-dotnet.sln`.
- Do not close P0-004, P0-005, P1, READY, or the active goal.

## Required Guard

Add one focused server test, proposed name:

```text
NaturalBattleResponseAllowsMultipleLegalSourcesSequentiallyBeforeAdvancement
```

Representative scenario:

1. Start from a natural `DECLARE_BATTLE` at `BF-DAMAGE` with `includeNextContest: true`, one P1 attacker, and one battle participant defender such as `BulwarkDefenderObjectId`.
2. Add two P2 controlled ready face-up Shadow sources on the same concrete battlefield. They should both be nonparticipants for the declared battle.
3. Give P2 enough payment resources for two Shadow activations, for example 2 mana and 2 generic power.
4. P1 declares battle with `COMBAT_ASSIGNMENT`.
5. Assert battle response priority opens on P2, assignment does not open, and `ACTIVATE_ABILITY` candidate exposes both Shadow sources.
6. P2 activates Shadow A targeting `AttackerObjectId`; assert one stack item, Shadow A exhausted, Shadow B still ready, `BF-NEXT` not advanced.
7. Pass-pass Shadow A stack; assert Shadow A resolves, battle remains active, priority returns to P2 battle response, Shadow A no longer appears as an enabled source, Shadow B still appears as enabled.
8. P2 activates Shadow B targeting the same attacker; assert a new stack item, Shadow B exhausted, `BF-NEXT` not advanced.
9. Pass-pass Shadow B stack; assert battle response priority returns again, both Shadows exhausted, no enabled Shadow source remains, and `BF-NEXT` still has not advanced.
10. P2/P1 pass the response window; assert final immediate battle close / control resolution order before `BF-NEXT` contest / spell duel advancement.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseAllowsMultipleLegalSourcesSequentiallyBeforeAdvancement"
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

This guard is expected to be test-only if prompt source filtering and battle response stack-return semantics already compose correctly. If runtime changes are required, keep them minimal and directly tied to response source eligibility or priority return.

Passing 4D-02W does not prove this slice: 4D-02W covers a nested standby reaction stack item from the opposing player, while 4D-02X must prove multiple independent legal battle-response sources owned by the response player across sequential activations.
