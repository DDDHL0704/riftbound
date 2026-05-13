# Stage 4D-01 Board Task Queue Foundation Evidence

日期：2026-05-13
结论：**FOUNDATION ACCEPTED / NOT READY**

本文记录 Stage 4D-01 board task queue foundation 的自动化证据。它验收 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md` 中列出的 focused acceptance checklist，但不把项目升级为 READY，也不关闭 P0-004、P0-005 或任何 P1 full-official 缺口。

## 1. Scope

- 实现/证据提交：`6a3ee038 test: add stage 4D board task queue foundation coverage`
- 修改文件：`tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`
- 本批没有修改 `src/Riftbound.Engine`、`src/Riftbound.Api`、前端、卡牌矩阵或文档以外的功能代码。
- 未跟踪的 `riftbound-dotnet.sln` 不属于本批证据。

## 2. New Coverage

新增 `BoardTaskQueueFoundationTests` 覆盖以下 4D-01 focused acceptance:

- 基地单位移入空战场：更新 `ObjectLocations` / `BattlefieldStates`，不产生 contest task。
- 基地单位移入敌方占据战场：进入 `SPELL_DUEL_TASKS`，pending queue 顺序为 `BATTLEFIELD_CONTESTED`、`START_SPELL_DUEL`、`START_BATTLE`。
- 致命清理优先于战场任务：blocking queue 下普通命令 rejected 且 no mutation，prompt 不泄露 raw task kind。
- 栈结算导致单位回基地后：contest 消失，queue 回到 `IDLE`。
- cleanup loop 重复到稳定：致命宿主离场后未贴附装备继续通过同一 cleanup loop 召回基地。
- 非法待命与未贴附装备：snapshot / prompt 隐藏未授权对象 id 和 raw cleanup reason。
- `PASS_FOCUS`：关闭 queued battlefield spell duel，并保留 participant controller/object data 推进 `START_BATTLE`。
- 精确战场 roam：保留 `BATTLEFIELD:<objectId>` 冒号后混合大小写，且只为目的地战场排 `START_SPELL_DUEL` / `START_BATTLE`。
- Reconnect / snapshot：pending cleanup phase、active task 与对手视角 illegal standby redaction 在重连后保持一致。

## 3. Commands

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~ReconnectWithPendingTaskQueue"
```

Result:

- Passed: 31
- Failed: 0
- Skipped: 0
- Total: 31

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~MoveUnit|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~Reconnect"
```

Result:

- Passed: 149
- Failed: 0
- Skipped: 0
- Total: 149

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

- Passed: 3780
- Failed: 0
- Skipped: 0
- Total: 3780

Diff hygiene:

```sh
git diff --check
git diff --no-index --check /dev/null tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs
```

Result: no whitespace output.

## 4. Interpretation

- 4D-01 focused board task queue foundation checklist is accepted at current HEAD.
- P0-002 / P0-003 are narrower than before because object location, battlefield states, pending task queue, cleanup blocking, destination-scoped contest tasks and reconnect redaction now have focused automated coverage.
- P0-002 / P0-003 are not declared full-official resolved here: complete held/conquer scoring, control freeze/release, replacement/prevention, every state-change route and official battle cleanup lifecycle still need later slices.
- P0-004 remains Stage 4D-02: spell duel / battle lifecycle is not complete official state-machine evidence.
- P0-005 remains Stage 4D-03: PaymentEngine unification is untouched.
- P1 LayerEngine / keyword / full-card evidence remains open.
