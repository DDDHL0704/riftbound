# Stage 4D-01C Battlefield Control Standby Cleanup Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 主控在 4D-01B 后继续收窄 P0-002 / P0-003 board/control lifecycle 的下一批服务端实现交接。它只锁定 battle close / battlefield control resolve 后，由战场控制变化导致的非法 standby cleanup 与后续 pending task ordering，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-01 foundation 已证明 cleanup-first queue、illegal standby redaction 与 pending battlefield task shape；4D-01B 已证明 control-change-to-battlefield 后保留 precise battlefield identity 并触发 destination-scoped contest tasks。但 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md` 的完整目标仍要求 battlefield control changes 与 standby removal 进入统一 post-state-change lifecycle。

当前代码面存在两个相邻口径：

- `RunStateBasedCleanupLoop` / `ApplyIllegalStandbyCleanup` 已有通用非法 standby cleanup。
- `ResolveBattlefieldControlAfterBattle` 另有 `RemoveIllegalStandbyAfterBattlefieldControl` 专门清理 battle control 后的非法 standby。

因此下一片需要用自然战斗流 dedicated guard 证明：battlefield control resolve 后的 standby cleanup 与 pending queue advancement 顺序一致，cleanup 不泄漏隐藏信息，不留下 stale battlefield / battle tasks。

## 2. Target Behavior

最小代表流程：

1. 有两个 concrete battlefields：`BF-A` 与 `BF-B`。
2. `BF-A` 进入自然 battle close / battlefield control resolve；P1 成为 `BF-A` controller。
3. `BF-A` 上存在 P2 的 face-down / standby card。控制权变为 P1 后，该 standby 非法。
4. 服务端在 `BATTLEFIELD_CONTROL_RESOLVED` 后清理该 standby：
   - 输出 `BATTLEFIELD_STANDBY_REMOVED`；
   - `reason == BATTLEFIELD_CONTROL_CLEANUP`；
   - removed standby 进入 owner graveyard；
   - `ObjectLocations[standby].Zone == "GRAVEYARD"`；
   - card object 变为 face-up / controller 回 owner；
   - player / spectator snapshot 与 prompt 不泄漏 face-down card identity 给非授权方。
5. 若同时 `BF-B` 已是 contested，cleanup 必须先于 `BF-B` 的 `START_SPELL_DUEL` / `START_BATTLE` advancement。
6. 最终不得残留 stale `START_BATTLE:BF-A`、stale battle prompt、stale cleanup task 或 stale precise battlefield location。

若当前 runtime 已满足，本切片可以 test-only。若不满足，最小修复应复用或收敛到已有 post-state-change cleanup loop，避免新增第二套 battlefield cleanup 语义。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- 或新增窄域测试文件：`tests/Riftbound.ConformanceTests/BattlefieldControlLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

仅 snapshot / prompt redaction gap 时允许写入：

- `src/Riftbound.Engine/MatchSession.cs`

禁止并行写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- battle lifecycle tests
- PaymentEngine tests
- frontend files
- card coverage matrix

## 4. Implementation Notes

- Prefer a dedicated failing guard first; if the guard is already green with current runtime, keep the slice test-only.
- If runtime changes are needed, preserve current battle close / control / held / conquer behavior and only normalize ordering around post-control cleanup and task advancement.
- Do not change hidden-information projection by exposing internal object ids, raw cleanup task ids, or face-down standby card details.
- Keep invalid / stale / wrong-player commands no-mutation while cleanup tasks block ordinary actions.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldControlLifecycle|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~Standby"
```

Focused acceptance should include at least:

- Natural battle close changes `BF-A` control and removes now-illegal standby at that battlefield.
- Removed standby enters owner graveyard with authoritative `ObjectLocations` reconciled.
- Snapshot / prompt redaction hides face-down standby identity from the opponent / spectator path.
- If `BF-B` is also contested, cleanup ordering is stable before `BF-B` spell-duel / battle task advancement.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 7. No-Go

- Do not rewrite full combat or full battle lifecycle.
- Do not start LayerEngine or PaymentEngine work.
- Do not modify frontend task UI.
- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not close P0-002, P0-003, P0-004, P0-005, P1, or READY.
- Do not mark active goal complete.
