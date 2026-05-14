# Stage 4D-02Z Battle Response Conquer Result Ordering Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02Z battle response conquer result ordering 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表 conquer result ordering after battle response 已显式覆盖。

## Baseline Validation

Targeted existing 4D-02Y guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationNoEffectForStaleTargetReturnsToResponseBeforeAdvancement"
```

Result:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
```

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Result:

```text
Passed: 280, Failed: 0, Skipped: 0, Total: 280
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 810, Failed: 0, Skipped: 0, Total: 810
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4222, Failed: 0, Skipped: 0, Total: 4222
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

The baseline proves current stale-target battle response, multiple-source response breadth, direct conquer / held fixture coverage, adjacent battlefield / prompt / stack regressions, and backend full suite are green at HEAD. It does not yet prove that a conquer result produced after a real battle response window is ordered before battle close, battlefield control resolution, and next contested battlefield advancement.
