# Stage 4D-01C Battlefield Control Standby Cleanup Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-01C 实现前基线。当前 HEAD 已有 illegal standby cleanup、battle damage assignment、board task queue、pending queue 与 battlefield control 相邻回归，但缺少本 handoff 指定的 natural battle-control -> illegal standby cleanup -> next battlefield task ordering dedicated guard。

## Baseline Scope

目标切片：`docs/CURRENT_STAGE4D_01C_BATTLEFIELD_CONTROL_STANDBY_CLEANUP_HANDOFF.md`

已检查关键事实：

- `RunStateBasedCleanupLoop` / `ApplyIllegalStandbyCleanup` 是通用非法 standby cleanup 入口。
- `ResolveBattlefieldControlAfterBattle` 另有 `RemoveIllegalStandbyAfterBattlefieldControl` 专门清理 battle control 后的非法 standby。
- 既有 `BoardTaskQueueFoundationTests` 覆盖 generic illegal standby redaction，但未证明 natural battle close / battlefield control resolve 后的 standby cleanup 与下一 contested battlefield task advancement 的 ordering。
- 既有 battle / control tests 覆盖 held / conquer / control representatives，但未作为 4D-01C 的 unified cleanup queue guard。

## Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldControlLifecycle|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~Standby"
```

Result:

```text
Passed: 123, Failed: 0, Skipped: 0, Total: 123
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub"
```

Result:

```text
Passed: 607, Failed: 0, Skipped: 0, Total: 607
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4203, Failed: 0, Skipped: 0, Total: 4203
```

## Baseline Verdict

The current suite is green but does not prove the new 4D-01C target. The implementing agent must add a dedicated failing guard or equivalent regression for battle-control-driven illegal standby cleanup and ordering before any remaining contested battlefield task advancement.

This baseline does not close P0-002, P0-003, P0-004, P0-005, P1, frontend gates, card matrix gates, or READY.
