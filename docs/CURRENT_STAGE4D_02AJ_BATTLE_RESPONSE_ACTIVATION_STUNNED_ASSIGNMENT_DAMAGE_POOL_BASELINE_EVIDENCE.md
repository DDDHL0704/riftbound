# Stage 4D-02AJ Battle Response Activation Stunned Assignment Damage Pool Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

This baseline records the current green surface before implementing 4D-02AJ. Existing tests prove the intended pieces independently:

- natural assignment prompt exposes `damagePool`, `legalTargets`, `lethalDamageThreshold`, and assignment choices;
- battle response pass-pass returns to `ASSIGN_COMBAT_DAMAGE`;
- actual Shadow activation / stack resolution / returned response can still return to assignment and later advance BF-B only after BF-A closes.

They do not yet prove the exact 4D-02AJ path: after Shadow stuns the attacker inside battle response, the returned assignment prompt and runtime validation must both use attacker damage pool 0, reject stale attacker-nonzero assignments, and still close BF-A / advance BF-B with a legal zero-attacker assignment.

## Validation

Targeted existing prerequisites:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalStartBattleWithAssignmentOrderingDefenderOpensAssignCombatDamagePrompt|FullyQualifiedName~NaturalBattleResponsePassThenOpensAssignCombatDamageForAssignmentOrderingBattle|FullyQualifiedName~NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask"
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
Passed: 292, Failed: 0, Skipped: 0, Total: 292
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~P79BattlefieldScoreDelay|FullyQualifiedName~P79BattlefieldScorePrevented|FullyQualifiedName~P79BattlefieldHeldScoreCanOnly"
```

Result:

```text
Passed: 822, Failed: 0, Skipped: 0, Total: 822
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4234, Failed: 0, Skipped: 0, Total: 4234
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Gap

The suite is green, but no guard currently asserts that activation-returned assignment damage pools are recalculated from a stunned attacker state. 4D-02AJ should add the missing prompt / validation contract guard and only fix runtime if the guard exposes a mismatch.
