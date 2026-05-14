# Stage 4D-02S Battle Lifecycle Remaining Scope Refresh Baseline Evidence

日期：2026-05-15
结论：**BASELINE GREEN / AUDIT-ONLY / PROJECT NOT READY**

本文记录 4D-02S remaining-scope refresh audit 的当前 HEAD 基线。它只证明当前 battle lifecycle 相邻测试绿色，支持 A 侧重新裁剪下一实现切片；不代表 P0-004 已关闭。

## Baseline Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Result:

```text
Passed: 231, Failed: 0, Skipped: 0, Total: 231
```

Adjacent:

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
Passed: 4216, Failed: 0, Skipped: 0, Total: 4216
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Baseline Notes

The current suite proves many representative battle lifecycle paths, but still leaves P0-004 open. This refresh audit recommends the next slice as activation-returned assignment plus battle-control-driven illegal standby cleanup ordering before next contested battlefield advancement.
