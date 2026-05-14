# Stage 4D-02W Battle Response Nested Standby Reaction Stack Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02W battle response nested standby reaction stack 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表 nested standby reaction inside battle response 已显式覆盖。

## Baseline Validation

Targeted existing 4D-02V guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationImmediateBattleSkipsCompletedCurrentBattlefieldBeforeAdvancingNextTask"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Focused current class baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests"
```

Result:

```text
Passed: 24, Failed: 0, Skipped: 0, Total: 24
```

Focused handoff baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Result:

```text
Passed: 277, Failed: 0, Skipped: 0, Total: 277
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 807, Failed: 0, Skipped: 0, Total: 807
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4219, Failed: 0, Skipped: 0, Total: 4219
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

The baseline proves the current Shadow battle-response single-stack surfaces, generic standby reaction stack surfaces, and adjacent prompt / battlefield / payment regressions are green at HEAD. It does not yet prove that a standby reaction can be added on top of a Shadow battle-response stack item and then correctly unwind back through battle response priority before next contested battlefield advancement.
