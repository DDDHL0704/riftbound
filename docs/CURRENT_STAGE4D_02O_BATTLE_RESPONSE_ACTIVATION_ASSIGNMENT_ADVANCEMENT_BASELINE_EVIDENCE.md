# Stage 4D-02O Battle Response Activation Assignment Advancement Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02O battle response activation assignment advancement 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表目标行为已覆盖。

## Baseline Validation

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 225, Failed: 0, Skipped: 0, Total: 225
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 748, Failed: 0, Skipped: 0, Total: 748
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4210, Failed: 0, Skipped: 0, Total: 4210
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

Existing tests cover actual Shadow activation / stack resolution in a natural battle response window, Brush context preservation through activation, and pass-only response -> assignment -> next contested battlefield advancement. They do not yet prove the combined path where actual Shadow activation resolves, returns to battle response, then enters `ASSIGN_COMBAT_DAMAGE`, and only after legal assignment advances the next contested battlefield.
