# Stage 4D-02AF Battle Response Activation Held-Score Next Contest Advancement Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

This baseline records the current green surface before implementing 4D-02AF. Existing tests prove the two sides of the intended cross-product independently:

- activation-returned held-score `RECYCLE_RUNE:*` and `TEMP_PAYMENT_RESOURCE:*` context preservation;
- activation-returned current battle close can advance the next contested battlefield.

They do not yet prove the combined path: activation-returned held-score payment / score / battle close followed by BF-B `START_SPELL_DUEL` advancement.

## Validation

Targeted existing cross-product prerequisites:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationPreservesHeldScorePaymentResourceContextAfterStackResolution|FullyQualifiedName~NaturalBattleResponseActivationPreservesHeldScoreTemporaryPaymentResourceContextAfterStackResolution|FullyQualifiedName~NaturalBattleResponseActivationImmediateBattleAdvancesNextContestedBattlefieldTask"
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
Passed: 288, Failed: 0, Skipped: 0, Total: 288
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 818, Failed: 0, Skipped: 0, Total: 818
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4230, Failed: 0, Skipped: 0, Total: 4230
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Gap

The suite is green, but the exact 4D-02AF path is not covered yet. The missing guard is a BF-A / BF-B cross-product where returned battle response resolves held-score payment on BF-A and then advances BF-B spell duel only after the score / battle-close branch completes.
