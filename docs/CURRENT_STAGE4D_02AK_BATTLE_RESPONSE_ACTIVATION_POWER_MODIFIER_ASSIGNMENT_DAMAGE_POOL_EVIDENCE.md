# 4D-02AK Battle Response Activation Power Modifier Assignment Damage Pool Evidence

日期：2026-05-15
结论：**ACCEPTED / evidence recorded / project NOT READY**

## Commands

Targeted new guard:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool"
```

Result: 1/1 passed.

Focused suite:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Result: 294/294 passed.

Adjacent suite:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~P79BattlefieldScoreDelay|FullyQualifiedName~P79BattlefieldScorePrevented|FullyQualifiedName~P79BattlefieldHeldScoreCanOnly"
```

Result: 824/824 passed.

Backend full:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4236/4236 passed.

Diff hygiene:

```bash
git diff --check
```

Result: no output / pass.

## Acceptance Notes

The accepted guard proves prompt metadata and runtime assignment validation now agree that a non-stunned participant's current `Power` is the effective assignment damage pool, even when `UntilEndOfTurnPowerModifier` is nonzero. The stale double-counted assignment shape is rejected without mutation, while the legal effective-power assignment closes BF-A before advancing BF-B. This evidence does not replace full P0/P1 closure, frontend smoke, formal E2E renewal, LayerEngine work, PaymentEngine breadth, or full-card official coverage.
