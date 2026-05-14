# Stage 4D-02AK Battle Response Activation Power Modifier Assignment Damage Pool Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

This baseline records the current green surface before implementing 4D-02AK. Existing tests prove the intended pieces independently:

- activation-returned assignment can open after actual Shadow activation / stack resolution / returned response;
- 02AJ proves stunned attacker damage pool is 0 in prompt and runtime validation;
- `NaturalBattleResponseActivationPostPaymentBlocksNextContestedBattlefieldUntilAccepted` proves an existing battle-flow path can leave a unit with `Power = 1` and `UntilEndOfTurnPowerModifier = -1`.

They do not yet prove the exact 4D-02AK path: a non-stunned battle participant with an until-end-of-turn power modifier must use current effective `Power` exactly once in returned assignment prompt metadata, runtime validation, and committed damage events.

## Validation

Targeted existing prerequisites:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationStunnedAttackerUsesZeroAssignmentDamagePool|FullyQualifiedName~NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask|FullyQualifiedName~NaturalBattleResponseActivationPostPaymentBlocksNextContestedBattlefieldUntilAccepted"
```

Result:

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
```

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Result:

```text
Passed: 293, Failed: 0, Skipped: 0, Total: 293
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~P79BattlefieldScoreDelay|FullyQualifiedName~P79BattlefieldScorePrevented|FullyQualifiedName~P79BattlefieldHeldScoreCanOnly"
```

Result:

```text
Passed: 823, Failed: 0, Skipped: 0, Total: 823
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4235, Failed: 0, Skipped: 0, Total: 4235
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Gap

The suite is green, but no guard currently asserts that activation-returned assignment damage pools use a non-stunned modified participant's effective `Power` once. 4D-02AK should add the missing prompt / validation / event-payload parity guard and only fix runtime if the guard exposes a mismatch.
