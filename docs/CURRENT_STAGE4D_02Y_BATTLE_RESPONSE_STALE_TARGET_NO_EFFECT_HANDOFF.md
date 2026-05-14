# Stage 4D-02Y Battle Response Stale Target No-Effect Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02X 已验收同一 battle response window 内两个 Shadow sources 的 sequential response representative。下一步继续 P0-004 battle-response breadth，但换到 stale / no-effect target axis。

Existing `ShadowActivatedAbilityTests.ShadowResolutionNoEffectsWhenTargetStopsAttacking` proves Shadow stack resolution has a basic no-effect guard when the target is no longer legal. 4D-02Y must prove the same no-effect resolution composes with active battle response priority:

- Shadow response stack item resolves to `ABILITY_NO_EFFECT` because the target stopped attacking before resolution.
- The battle remains active and returns to battle response priority.
- `BF-NEXT` does not advance during stale stack resolution or returned response priority.
- Final response close still follows normal battle close / control resolution / next contested battlefield ordering.

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
NaturalBattleResponseActivationNoEffectForStaleTargetReturnsToResponseBeforeAdvancement
```

Representative scenario:

1. Start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId])`.
2. P1 declares battle at `BF-DAMAGE` with `COMBAT_ASSIGNMENT`.
3. P2 activates Shadow targeting `AttackerObjectId`; assert one stack item and no `BF-NEXT` advancement.
4. Before pass-pass stack resolution, make the target stale in the test state by setting `AttackerObjectId.IsAttacking = false`, matching the existing `ShadowResolutionNoEffectsWhenTargetStopsAttacking` pattern.
5. P2/P1 pass the Shadow stack.
6. Assert:
   - stack is empty;
   - `ABILITY_RESOLVED` then `ABILITY_NO_EFFECT` appears with `reason = TARGET_NO_LONGER_LEGAL`;
   - attacker does not gain `STUNNED`;
   - battle remains active;
   - timing is `NeutralClosed`;
   - priority returns to P2 battle response;
   - `BF-NEXT` has not advanced.
7. P2/P1 pass response priority.
8. Assert final immediate battle close ordering before `BF-NEXT`:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED`
   - `BATTLE_CLOSED`
   - `BATTLEFIELD_CONTROL_RESOLVED`
   - `BATTLEFIELD_CONTESTED` for `BF-NEXT`
   - `SPELL_DUEL_STARTED` for `BF-NEXT`

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationNoEffectForStaleTargetReturnsToResponseBeforeAdvancement"
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

This is expected to be test-only. Runtime changes are allowed only if the active battle response stack-return path mishandles no-effect resolution or advances `BF-NEXT` early.

Passing the existing standalone Shadow stale-target test is not enough; 4D-02Y must prove the stale no-effect branch inside active battle response priority.
