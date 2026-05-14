# Stage 4D-02AC Battle Response Activation Held Result Ordering Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02AB 已验收 actual Shadow activation / stack resolution / returned response -> assignment -> Hunt conquer result ordering。4D-02AC 补上对称的 activation-returned held branch，避免 battle result ordering 只在 activation-returned conquer 分支有显式 guard。

4D-02AC 锁定一个窄的 activation-returned held representative：

- natural battle 先打开 battle response priority；
- P2 从该 response window 真实 activation Shadow，进入 stack；
- stack pass-pass 解析后回到 battle response priority；
- 双方 pass response 后进入 `ASSIGN_COMBAT_DAMAGE`；
- 合法 assignment 让带 Hunt 的防守方守住当前 battlefield；
- `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` 必须先于 `DAMAGE_REMOVED`、`BATTLE_CLOSED`、`BATTLEFIELD_CONTROL_RESOLVED` 与下一处 contested battlefield 推进；
- stack-open、stack-resolved returned response、assignment window 和 result ordering 期间均不得提前推进 `BF-NEXT`。

This composes existing 4D-02O activation-returned assignment advancement with 4D-02AA held result ordering.

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
NaturalBattleResponseActivationAssignmentHeldResultOrdersBeforeNextAdvancement
```

Representative scenario:

1. Start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId, BackRowDefenderObjectId])`.
2. Modify `AttackerObjectId` to be a 1-power non-Hunt attacker: power 1, tags `[CardObjectTags.UnitCard]`.
3. Modify `BulwarkDefenderObjectId` to be the surviving Hunt defender: card no `UNL-059/219`, power 2, tags `[CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark, "狩猎2"]`.
4. P1 declares battle at `BF-DAMAGE` with attackers `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId, BackRowDefenderObjectId]`, and optional cost `COMBAT_ASSIGNMENT`.
5. Assert battle response priority opens for P2 and `BF-NEXT` has not advanced.
6. P2 activates Shadow targeting `AttackerObjectId`; assert stack item is added and `BF-NEXT` has not advanced.
7. P2 / P1 pass stack priority; assert Shadow resolves, attacker is stunned, battle remains active, priority returns to P2 response, and `BF-NEXT` has not advanced.
8. P2 / P1 pass returned response priority; assert:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED` precedes `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
   - assignment prompt is open for P1;
   - `BF-NEXT` has not advanced.
9. P1 assigns combat damage so the attacker is destroyed and both defenders survive:
   - `AttackerObjectId -> BulwarkDefenderObjectId` for 1;
   - `BulwarkDefenderObjectId -> AttackerObjectId` for 2;
   - `BackRowDefenderObjectId -> AttackerObjectId` for 1.
10. Assert assignment result event ordering:
   - `BATTLEFIELD_HELD` for `BattlefieldObjectId`, player `P2`, Hunt source `BulwarkDefenderObjectId`;
   - `EXPERIENCE_GAINED` for P2 and the Hunt defender source;
   - `DAMAGE_REMOVED` cleanup;
   - `BATTLE_CLOSED`;
   - `BATTLEFIELD_CONTROL_RESOLVED`;
   - `BATTLEFIELD_CONTESTED` for `NextBattlefieldObjectId`;
   - `SPELL_DUEL_STARTED` for `NextBattlefieldObjectId`.
11. Assert no `BATTLEFIELD_CONQUERED` appears in this held branch, the current `START_BATTLE` task is gone, and the next active task is `task:start-spell-duel:BF-NEXT`.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationAssignmentHeldResultOrdersBeforeNextAdvancement"
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

This is expected to be test-only after 4D-02AA. Runtime changes are allowed only if actual activation-returned assignment loses held result context, orders result / cleanup / close / control / next-task events incorrectly, or advances `BF-NEXT` early.

Passing 4D-02O or 4D-02AA alone is not enough; 4D-02AC must prove their cross-product after real Shadow activation and stack resolution.
