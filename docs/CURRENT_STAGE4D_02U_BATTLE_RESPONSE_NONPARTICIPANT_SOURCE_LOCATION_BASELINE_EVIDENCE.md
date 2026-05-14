# Stage 4D-02U Battle Response Nonparticipant Source Location Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02U battle response nonparticipant source location preservation 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表目标行为已覆盖。

## Baseline Validation

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 232, Failed: 0, Skipped: 0, Total: 232
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 754, Failed: 0, Skipped: 0, Total: 754
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4217, Failed: 0, Skipped: 0, Total: 4217
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

Existing 4D-02T runtime evidence preserves precise battlefield identity for an active battle participant stack source. Existing 4D-02P / 4D-02Q guards exercise Shadow as a nonparticipant battle response source, but they do not assert that stack resolution preserves `ObjectLocations[ShadowObjectId].BattlefieldObjectId`. This handoff establishes that missing guard as the next narrow P0-004 slice.
