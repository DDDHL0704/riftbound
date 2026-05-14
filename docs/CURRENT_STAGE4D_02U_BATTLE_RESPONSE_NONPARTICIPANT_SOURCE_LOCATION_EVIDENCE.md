# Stage 4D-02U Battle Response Nonparticipant Source Location Evidence

日期：2026-05-15
结论：**TARGETED / FOCUSED / ADJACENT / FULL GREEN / PROJECT NOT READY**

## Validation

Whitespace:

```sh
git diff --check
```

Result: no output.

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationPreservesNonParticipantSourceBattlefieldLocationAfterStackResolution"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Targeted adjusted advancement guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 233, Failed: 0, Skipped: 0, Total: 233
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 755, Failed: 0, Skipped: 0, Total: 755
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4218, Failed: 0, Skipped: 0, Total: 4218
```

## Notes

The focused guard proves a nonparticipant Shadow response source preserves its concrete battlefield id after stack resolution returns to battle response priority. The slice deliberately does not decide final battle-close behavior when such a nonparticipant source remains on the current battlefield.
