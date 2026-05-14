# Stage 4D-02W Battle Response Nested Standby Reaction Stack Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02V 已验收 battle response 中非参战 Shadow 保留 precise battlefield location 后的 completed-current-battlefield skip policy。下一步不要继续扩同一 Shadow location 分支，而应回到 P0-004 battle lifecycle breadth。

本切片锁定一个不同轴的 battle-response stack breadth guard：在自然 battle response window 中，P2 先用 Shadow swift stun 创建一个 stack item；P2 让过后，P1 在同一个由 battle response 派生出的 stack priority window 中用 face-down standby reaction 再创建第二个 stack item。验收重点是 LIFO 结算、nested reaction resolved 后仍回到原 Shadow stack item，再回到 battle response priority，且整个过程中不得提前推进下一处 contested battlefield。

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
NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse
```

Representative scenario:

1. Start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId])`.
2. Extend the fixture with a P1 controlled face-down standby source in base, preferably existing reveal-card representative `OGN·121/298` or `OGN·197/298`.
3. P1 declares battle at `BF-DAMAGE` with attacker vs `BulwarkDefenderObjectId` and `COMBAT_ASSIGNMENT`.
4. P2 activates Shadow swift stun targeting `AttackerObjectId`.
5. Assert Shadow creates one stack item, timing remains `NeutralClosed`, priority starts on P2, and `BF-NEXT` has not advanced.
6. P2 passes stack priority.
7. Assert P1 has stack priority and prompt / candidates expose `REVEAL_CARD` for the face-down standby source.
8. P1 reveals the standby as:

```text
Mode: STANDBY_REACTION
Destination: STACK
OptionalCosts: [STANDBY_REVEAL_0]
```

9. Assert two stack items exist in order: original Shadow item below, standby reaction item on top; no `BATTLE_RESPONSE_PRIORITY_CLOSED`, no `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, no `BF-NEXT` advancement.
10. P1 then P2 pass; standby reaction resolves first, source returns to P1 base face-up, Shadow item remains on stack, priority returns to P2, and `BF-NEXT` still has not advanced.
11. P2 then P1 pass; Shadow resolves, attacker is stunned, stack becomes empty, battle remains active, priority returns to battle response owner order, and `BF-NEXT` still has not advanced.
12. P2/P1 pass the battle response window; then final immediate battle close / control resolution may advance `BF-NEXT`, but only after:
    - `BATTLE_RESPONSE_PRIORITY_CLOSED`
    - `BATTLE_CLOSED`
    - `BATTLEFIELD_CONTROL_RESOLVED`
13. Assert final state is `SPELL_DUEL_TASKS` on `BF-NEXT`, not assignment prompt or stale battle declaration prompt.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse"
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

Existing P4 standby reaction tests already prove generic stack nesting outside battle, and 4D-02P through 4D-02V prove single Shadow stack return / battle-response advancement branches. This slice must combine those surfaces under an active battle response window. Passing either existing family alone is not enough.

The expected outcome is likely test-only. If runtime changes are needed, they must be minimal and directly tied to stack priority / battle response return semantics.
