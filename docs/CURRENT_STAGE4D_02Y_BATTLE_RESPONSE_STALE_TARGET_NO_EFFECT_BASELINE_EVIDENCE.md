# Stage 4D-02Y Battle Response Stale Target No-Effect Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02Y battle response stale target no-effect 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表 stale target no-effect inside battle response 已显式覆盖。

## Baseline Validation

Targeted existing 4D-02X guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseAllowsMultipleLegalSourcesSequentiallyBeforeAdvancement"
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
Passed: 279, Failed: 0, Skipped: 0, Total: 279
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 809, Failed: 0, Skipped: 0, Total: 809
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4221, Failed: 0, Skipped: 0, Total: 4221
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

The baseline proves current Shadow stale-target standalone behavior, battle-response stack-return paths, multiple-source response breadth, and adjacent battlefield / prompt / stack regressions are green at HEAD. It does not yet prove that stale no-effect Shadow resolution inside active battle response priority returns to response before next battlefield advancement.
