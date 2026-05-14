# Stage 4D-02AH Battle Response Activation Brush Next Contest Advancement Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

This baseline records the current green surface before implementing 4D-02AH. Existing tests prove the intended cross-product pieces independently:

- natural battle response preserves Brush replacement context after pass-pass;
- actual Shadow activation / stack resolution / returned response preserves Brush replacement context;
- activation-returned held-score temporary payment / score / battle close advances BF-B only after BF-A closes;
- activation-returned held-score recycle payment / score / battle close advances BF-B only after BF-A closes.

They do not yet prove the combined replacement path: actual Shadow activation / stack resolution / returned response -> Brush replacement-aware held-score payment / score / BF-A battle close -> BF-B `START_SPELL_DUEL` advancement.

## Validation

Targeted existing cross-product prerequisites:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponsePreservesBrushReplacementContextAfterPass|FullyQualifiedName~NaturalBattleResponseActivationPreservesBrushReplacementContextAfterStackResolution|FullyQualifiedName~NaturalBattleResponseActivationHeldScoreTemporaryPaymentAdvancesNextContestedBattlefieldTask|FullyQualifiedName~NaturalBattleResponseActivationHeldScoreRecyclePaymentAdvancesNextContestedBattlefieldTask"
```

Result:

```text
Passed: 4, Failed: 0, Skipped: 0, Total: 4
```

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Result:

```text
Passed: 290, Failed: 0, Skipped: 0, Total: 290
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 820, Failed: 0, Skipped: 0, Total: 820
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4232, Failed: 0, Skipped: 0, Total: 4232
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Gap

The suite is green, but the exact 4D-02AH path is not covered yet. The missing guard is a BF-A / BF-B cross-product where returned battle response resolves Brush replacement-aware held-score payment on BF-A and then advances BF-B spell duel only after replacement, score, and BF-A battle close complete.
