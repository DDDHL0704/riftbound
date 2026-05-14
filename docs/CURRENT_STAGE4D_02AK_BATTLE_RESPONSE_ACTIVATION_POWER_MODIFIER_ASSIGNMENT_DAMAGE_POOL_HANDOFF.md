# Stage 4D-02AK Battle Response Activation Power Modifier Assignment Damage Pool Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02AJ 已验收 activation-returned assignment 中 stunned attacker 的 damage pool 为 0。4D-02AK 转向同一个 prompt / runtime contract 的另一个 P0-004 风险点：非眩晕参战单位带有 until-end-of-turn power modifier 时，assignment prompt、runtime validation 与 committed damage pool 必须使用当前有效战力一次，而不是把 `UntilEndOfTurnPowerModifier` 再叠加一次。

当前模型约定：

- `CardObjectState.Power` 是当前有效战力；
- public object view 暴露 `effectivePower = cardObject.Power`；
- `basePower = Power - UntilEndOfTurnPowerModifier`；
- `ContinuousEffectState` 也以 `cardObject.Power` 作为 effective power。

当前代表性风险点：

- `MatchSession.BattleEffectivePowerFor(...)` 仍可能使用 `cardObject.Power + cardObject.UntilEndOfTurnPowerModifier`；
- `CoreRuleEngine.BuildCombatDamagePool(...)` / `BuildCombatLethalDamageThreshold(...)` 仍可能使用同样的 double-count 公式；
- 02AJ 只修复 stunned participant 为 0，没有证明非眩晕 modified participant 的 prompt / accepted command / event payload 与 public effective power 一致。

4D-02AK 要把这个差异锁成一个 server-only focused guard：真实 Shadow activation / stack resolution / returned response 后，一个非眩晕 defender 带 `Power = 1`、`UntilEndOfTurnPowerModifier = -1` 时，assignment prompt 与 runtime 仍按 effective power 1 分配伤害，而不是 double-count 成 0。

## Owner

B 服务端规则 / 协议 / 测试实现：Raman `019e2257-8d40-7630-9201-28df44dd689a`

## Write Scope

Primary expected write:

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

Allowed only if the new guard exposes the expected prompt / runtime mismatch:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

No-go:

- Do not modify frontend.
- Do not broaden PaymentEngine, LayerEngine, card behavior registry, or full card matrix.
- Do not solve every continuous-effect / damage-modification / replacement interaction in this slice.
- Do not touch `riftbound-dotnet.sln`.
- Do not close P0-004, P0-005, P1, READY, or the active goal.

## Required Guard

Add one focused server guard, proposed name:

```text
NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool
```

Representative scenario:

1. Start from the existing natural battle response assignment setup:
   - `includeShadowResponse: true`;
   - `includeNextContest: true`;
   - defenders include `BulwarkDefenderObjectId` and `ShadowObjectId`;
   - BF-A is the active `START_BATTLE`;
   - BF-B is contested and waiting for later `START_SPELL_DUEL` advancement.
2. Before declaration, seed `BulwarkDefenderObjectId` as a non-stunned modified participant:
   - `Power = 1`;
   - `UntilEndOfTurnPowerModifier = -1`;
   - no `STUNNED` effect.
   This means base power 2 / effective power 1 under the current object-view contract.
3. P1 declares battle on BF-A with attacker `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId, ShadowObjectId]`, optional costs `["COMBAT_ASSIGNMENT"]`.
4. Assert initial response priority opens for P2 and BF-B does not advance.
5. P2 activates Shadow targeting `AttackerObjectId`; assert normal activation, exhaustion, cost payment, stack item, and no BF-B advancement.
6. P2 / P1 pass stack; assert Shadow resolves, attacker has `STUNNED`, modified Bulwark remains non-stunned with `Power == 1` and `UntilEndOfTurnPowerModifier == -1`, battle remains active, returned response priority is P2, and no BF-B advancement.
7. P2 / P1 pass returned response priority.
8. Assert returned assignment prompt metadata:
   - P1 prompt is `AssignCombatDamage`;
   - `damagePool[AttackerObjectId] == 0`;
   - `damagePool[BulwarkDefenderObjectId] == 1`;
   - `damagePool[ShadowObjectId] == 1`;
   - `lethalDamageThreshold[BulwarkDefenderObjectId] == 1` when the defender has no existing damage;
   - Bulwark `battleParticipants[*].power == 1`;
   - `requiredAssignments` includes Bulwark with damage 1 and Shadow with damage 1;
   - `assignmentChoices` still offers `BulwarkDefenderObjectId -> AttackerObjectId`.
9. Submit the stale double-counted assignment shape that omits Bulwark damage, for example only `ShadowObjectId -> AttackerObjectId` for 1, and assert rejection/no mutation:
   - result is not accepted;
   - error is an invalid payload / assignment-pool mismatch;
   - state tick, battle state, stack, prompts, and BF-B pending task remain unchanged.
10. Submit a legal effective-power assignment:
    - `BulwarkDefenderObjectId -> AttackerObjectId` for 1;
    - `ShadowObjectId -> AttackerObjectId` for 1.
11. Assert assignment commit:
    - `COMBAT_DAMAGE_ASSIGNED.damagePool[BulwarkDefenderObjectId] == 1`;
    - Bulwark `DAMAGE_APPLIED.sourceDamagePool == 1`;
    - Shadow `DAMAGE_APPLIED.sourceDamagePool == 1`;
    - no attacker-sourced damage is applied because attacker is stunned;
    - BF-A battle closes only after assignment commit / result handling;
    - BF-B `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_STARTED` happen only after BF-A closes;
    - final prompt is `SpellDuelFocus` for `NextBattlefieldObjectId`;
    - no stale BF-A assignment / battle declaration prompt remains.

## Runtime Shape If Needed

If the guard exposes the expected mismatch, keep the runtime change narrow:

- In assignment prompt metadata, after stunned handling, use `cardObject.Power` as the current effective combat power.
- In assignment runtime validation, after stunned handling, use `cardObject.Power` for damage pool and `cardObject.Power - cardObject.Damage` for lethal threshold.
- Keep 02AJ stunned semantics intact.
- Do not refactor the broader LayerEngine or card behavior modifier pipeline in this slice.

The most important invariant is that object view, prompt contract, accepted command contract, and committed combat-damage event payload all agree on the same effective power.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool"
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

This slice should be test-first. Runtime edits are allowed only to align assignment prompt metadata and runtime validation with the already documented effective-power model. Keep the representative narrow: this is not the full continuous-effect, prevention, replacement, LayerEngine, frontend, or card-matrix closure.
