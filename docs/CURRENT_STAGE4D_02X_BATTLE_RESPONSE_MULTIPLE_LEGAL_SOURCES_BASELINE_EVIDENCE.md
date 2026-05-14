# Stage 4D-02X Battle Response Multiple Legal Sources Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02X battle response multiple legal sources 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表 multiple independent response sources 已显式覆盖。

## Baseline Validation

Targeted existing 4D-02W guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse"
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
Passed: 278, Failed: 0, Skipped: 0, Total: 278
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 808, Failed: 0, Skipped: 0, Total: 808
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4220, Failed: 0, Skipped: 0, Total: 4220
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

The baseline proves current Shadow single-source response paths, nested standby reaction stack breadth, and adjacent battlefield / prompt / stack regressions are green at HEAD. It does not yet prove that two independent ready Shadow sources are both exposed, consumed one at a time, and kept from advancing `BF-NEXT` until the response window fully closes.
