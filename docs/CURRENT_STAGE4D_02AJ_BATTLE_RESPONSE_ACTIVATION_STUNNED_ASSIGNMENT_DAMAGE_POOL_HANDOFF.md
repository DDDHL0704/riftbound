# Stage 4D-02AJ Battle Response Activation Stunned Assignment Damage Pool Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02AI 已验收 activation-returned held-score score-prevention / next-contested advancement。4D-02AJ 转向一个不同的 P0-004 轴：battle response effect 返回 `ASSIGN_COMBAT_DAMAGE` 后，assignment prompt 与 runtime validation 必须使用已经被 response 改变后的 combat state。

当前代表性风险点：

- immediate combat branch 的 `ResolveBattleCombatPower(...)` 已把 `STUNNED` 单位视为 combat power 0；
- assignment prompt metadata 通过 `BattleDamagePoolFor(...)` / `BattleEffectivePowerFor(...)` 暴露 `damagePool`；
- assignment runtime validation 通过 `BuildCombatDamagePool(...)` / `BuildCombatLethalDamageThreshold(...)` 校验提交；
- 现有 activation-returned assignment guards 只证明 Shadow stun 后能回到 assignment window 和 next-contested advancement，没有证明 stunned attacker 的 damage pool 为 0，也没有证明旧的 attacker nonzero damage assignment 会被拒绝。

4D-02AJ 要把这个差异锁成一个 server-only focused guard：真实 Shadow activation / stack resolution / returned response 后，stunned attacker 在 assignment prompt 和 runtime damage pool 中都必须是 0，且后续合法 zero-attacker assignment 才能关闭 BF-A 并推进 BF-B。

## Owner

B 服务端规则 / 协议 / 测试实现：Raman `019e2257-8d40-7630-9201-28df44dd689a`

## Write Scope

Primary expected write:

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

Allowed only if the new guard exposes the expected runtime/prompt mismatch:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

No-go:

- Do not modify frontend.
- Do not broaden PaymentEngine, LayerEngine, or full card matrix.
- Do not solve generic power-modifier / continuous-effect breadth in this slice unless required by the stunned assignment guard.
- Do not touch `riftbound-dotnet.sln`.
- Do not close P0-004, P0-005, P1, READY, or the active goal.

## Required Guard

Add one focused server guard, proposed name:

```text
NaturalBattleResponseActivationStunnedAttackerUsesZeroAssignmentDamagePool
```

Representative scenario:

1. Start from the existing natural battle response assignment setup:
   - `includeShadowResponse: true`;
   - `includeNextContest: true`;
   - defenders include `BulwarkDefenderObjectId` and `ShadowObjectId`;
   - BF-A is the active `START_BATTLE`;
   - BF-B is contested and waiting for later `START_SPELL_DUEL` advancement.
2. P1 declares battle on BF-A with attacker `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId, ShadowObjectId]`, optional costs `["COMBAT_ASSIGNMENT"]`.
3. Assert initial response priority opens for P2 and BF-B does not advance.
4. P2 activates Shadow targeting `AttackerObjectId`; assert normal activation, exhaustion, cost payment, stack item, and no BF-B advancement.
5. P2 / P1 pass stack; assert Shadow resolves, attacker has `STUNNED`, battle remains active, returned response priority is P2, and no BF-B advancement.
6. P2 / P1 pass returned response priority.
7. Assert returned assignment prompt metadata:
   - P1 prompt is `AssignCombatDamage`;
   - `damagePool[AttackerObjectId] == 0`;
   - `lethalDamageThreshold[AttackerObjectId] == 0`;
   - attacker `battleParticipants[*].power == 0`;
   - `requiredAssignments` and `assignmentChoices` do not require or offer nonzero attacker damage;
   - defender sources still retain their expected damage pools.
8. Submit the old pre-02AJ attacker-nonzero assignment shape, for example the current `ShadowResponseLegalAssignments()` with attacker damage 5, and assert rejection/no mutation:
   - result is not accepted;
   - error is an invalid payload / assignment-pool mismatch;
   - state tick, battle state, stack, prompts, and BF-B pending task remain unchanged.
9. Submit a legal stunned-attacker assignment with no attacker damage, for example:
   - `BulwarkDefenderObjectId -> AttackerObjectId` for 2;
   - `ShadowObjectId -> AttackerObjectId` for 1.
10. Assert assignment commit:
    - `COMBAT_DAMAGE_ASSIGNED.damagePool[AttackerObjectId] == 0`;
    - no `DAMAGE_APPLIED` event has `sourceObjectId == AttackerObjectId`;
    - defender `DAMAGE_APPLIED` events include `sourceDamagePool` 2 and 1;
    - BF-A battle closes only after assignment commit / result handling;
    - BF-B `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_STARTED` happen only after BF-A closes;
    - final prompt is `SpellDuelFocus` for `NextBattlefieldObjectId`;
    - no stale BF-A assignment / battle declaration prompt remains.

## Runtime Shape If Needed

If the guard exposes the expected mismatch, keep the runtime change narrow:

- Align `MatchSession` prompt metadata damage pool / lethal threshold with the server validation semantics.
- Align `CoreRuleEngine` assignment validation damage pool / lethal threshold with immediate combat semantics for stunned participants.
- Avoid broad refactors of official combat, arbitrary modifiers, LayerEngine, keyword matrix, or card behavior registry in this slice.

The most important invariant is that the prompt contract and accepted command contract agree. A client must not be shown attacker damage that the server rejects, and the server must not accept attacker damage that the prompt should not offer.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationStunnedAttackerUsesZeroAssignmentDamagePool"
```

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~P79BattlefieldScoreDelay|FullyQualifiedName~P79BattlefieldScorePrevented|FullyQualifiedName~P79BattlefieldHeldScoreCanOnly"
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

This slice should be test-first. Runtime edits are allowed only to make prompt metadata and assignment validation agree on stunned combat power. Keep the representative narrow: this is not the full power-modifier, prevention, replacement, LayerEngine, frontend, or card-matrix closure.
