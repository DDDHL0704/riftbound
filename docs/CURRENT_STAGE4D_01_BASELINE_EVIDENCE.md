# Stage 4D-01 Board Task Queue Baseline Evidence

日期：2026-05-13
结论：**BASELINE RECORDED / NOT READY**

本文记录 4D-01 board task queue foundation 实现前的当前 HEAD 基线。它不是 P0-002 / P0-003 的完成证据，只用于后续验收对照。

## 1. Baseline Scope

- 代码状态：`fb4c1b98 docs: record stage 4D board task handoff checkpoint` 之后。
- 本轮不修改功能代码、测试代码、前端代码或 coverage matrix。
- 目的：确认现有 `PendingTaskQueue`、battlefield task guard、move/cleanup/spell-duel/start-battle 周边测试在进入 4D-01 实现前保持绿色。

## 2. Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PendingTaskQueue"
```

Result:

- Passed: 22
- Failed: 0
- Skipped: 0
- Total: 22

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~MoveUnit|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle"
```

Result:

- Passed: 139
- Failed: 0
- Skipped: 0
- Total: 139

## 3. Interpretation

- Existing representative queue/battlefield/move/cleanup/spell-duel/start-battle tests are green at current HEAD.
- The focused filter currently matches existing `BattlefieldContestBattleTaskGuardTests` and `PendingTaskQueue` coverage; no `BoardTaskQueueFoundationTests` file exists yet.
- This baseline does not prove that all state changes enter one durable board task lifecycle.
- This baseline does not close P0-002, P0-003, P0-004, P0-005 or any P1 item.
- Final 4D-01 acceptance still requires the handoff checklist in `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md`, broader adjacent regression, backend full test, diff review and A audit.
