# Stage 4D-02L Immediate Battle Task Advancement Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02L immediate battle task advancement 实现前基线。它只证明当前相邻测试绿色，可作为下一实现切片的回归护栏；不代表目标行为已覆盖。

## Baseline Validation

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle"
```

Result:

```text
Passed: 113, Failed: 0, Skipped: 0, Total: 113
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub"
```

Result:

```text
Passed: 608, Failed: 0, Skipped: 0, Total: 608
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4204, Failed: 0, Skipped: 0, Total: 4204
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

Existing tests already cover active `START_BATTLE` prompt shape, invalid `DECLARE_BATTLE` no-mutation, representative immediate battle close, assignment-branch next contested advancement, and battle-control-driven standby cleanup ordering. They do not yet contain a dedicated immediate `DECLARE_BATTLE` next contested battlefield advancement guard.
