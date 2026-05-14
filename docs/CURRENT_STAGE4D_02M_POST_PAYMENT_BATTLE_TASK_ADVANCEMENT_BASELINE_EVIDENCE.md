# Stage 4D-02M Post-Payment Battle Task Advancement Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02M post-payment battle task advancement 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表目标行为已覆盖。

## Baseline Validation

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests"
```

Result:

```text
Passed: 175, Failed: 0, Skipped: 0, Total: 175
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST"
```

Result:

```text
Passed: 702, Failed: 0, Skipped: 0, Total: 702
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4206, Failed: 0, Skipped: 0, Total: 4206
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

Existing tests cover immediate battle task advancement and existing trigger payment open / accept / decline behavior independently. They do not yet prove that a post-battle trigger payment window suppresses next contested battlefield advancement while open and resumes that advancement after the payment window closes.
