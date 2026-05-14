# Stage 4D-01B Control Change Precise Battlefield Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-01B 实现前基线。当前 HEAD 已有 Hostile Takeover、object location、pending queue、board task foundation 与 adjacent battle task 回归，但缺少本 handoff 指定的 control-change precise battlefield guard。

## Baseline Scope

目标切片：`docs/CURRENT_STAGE4D_01B_CONTROL_CHANGE_PRECISE_BATTLEFIELD_HANDOFF.md`

已检查关键事实：

- `HostileTakeoverGuardTests` 只证明 Hostile Takeover 获得控制、ready、安排回合结束归还，以及目标 guard；未证明 control-change 后保留 precise `BattlefieldObjectId`。
- `ReconcileObjectLocations` 当前仅在 playerId 不变的 battlefield-zone 对象上保留 precise battlefield id。
- stack resolution 已在结算后运行 `RunStateBasedCleanupLoop` 与 `AdvancePendingBattlefieldTasksAfterStateChange`，因此本切片可以通过修正位置保留让既有 task advancement 自然接管。

## Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~ObjectLocation|FullyQualifiedName~BattlefieldState|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BoardTaskQueueFoundation"
```

Result:

```txt
Passed: 32/32
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~MoveUnit|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~ObjectLocation"
```

Result:

```txt
Passed: 173/173
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```txt
Passed: 4201/4201
```

## Baseline Verdict

The current suite is green but does not prove the new 4D-01B target. The implementing agent must add a dedicated failing guard or equivalent regression for control-change preserving precise battlefield identity and triggering destination-scoped contest tasks.

This baseline does not close P0-002, P0-003, P0-004, P0-005, P1, frontend gates, card matrix gates, or READY.
