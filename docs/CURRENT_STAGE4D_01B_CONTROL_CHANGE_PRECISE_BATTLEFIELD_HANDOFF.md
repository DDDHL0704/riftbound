# Stage 4D-01B Control Change Precise Battlefield Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 主控在 4D-02K 后重新回到 P0-002 / P0-003 board/control lifecycle 的下一批服务端实现交接。它只锁定 control-change 后的精确战场位置与 pending task 触发，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-01 foundation 已证明 move / cleanup / reconnect / pending queue 的代表路径绿色，4D-02B-K 已证明若干 natural battle-response representative。但 `docs/CURRENT_SERVER_RULE_AUDIT.md` 仍明确把完整 battlefield / standby / control / held / conquer task 状态机列为 RISKY。

本切片选择一个最小但高价值缺口：

- `CoreRuleEngine.ReconcileObjectLocations` 当前只有在 `currentLocation.PlayerId == new playerId` 时才保留 `BattlefieldObjectId`。
- Hostile Takeover / gain-control-to-battlefield 这类 control-change path 会把目标从原控制者 battlefield zone 移到新控制者 battlefield zone。
- 如果精确 `BattlefieldObjectId` 在 playerId 改变时丢失，`BattlefieldStates` / `BattlefieldTasks` / pending cleanup queue 无法稳定知道该单位仍位于哪个具体战场。
- 这会削弱后续 control / contest / held / conquer task lifecycle 的服务端权威依据。

## 2. Target Behavior

最小代表流程：

1. P2 控制战场 `BF-CONTROL`，该战场上有两个 P2 正面单位。
2. P1 打出 Hostile Takeover / `SFD·202/221`，目标是其中一个 P2 单位。
3. 双方 priority pass，栈顶 Hostile Takeover 结算。
4. 被夺取单位：
   - owner 仍为 P2；
   - controller 变为 P1；
   - 仍在 battlefield zone；
   - `ObjectLocations[target].Zone == "BATTLEFIELD"`；
   - `ObjectLocations[target].BattlefieldObjectId == "BF-CONTROL"`，不得丢失为 null。
5. 因同一具体战场上同时存在 P1 / P2 控制的正面单位，服务端必须从 authoritative state 派生 contest：
   - `BattlefieldStates["BF-CONTROL"].Contested == true`；
   - occupant controller ids 包含 P1 和 P2；
   - pending queue 进入 `SPELL_DUEL_TASKS`；
   - active task 为 `task:start-spell-duel:BF-CONTROL`；
   - tasks 包含 `BATTLEFIELD_CONTESTED`、`START_SPELL_DUEL`、`START_BATTLE`；
   - events 包含 `BATTLEFIELD_CONTESTED` 与 `SPELL_DUEL_STARTED`。

Secondary guard 可选但推荐：

- 单目标 control-change 后若具体战场没有敌对 occupant，不应凭 battlefield object controller 误开 contest；但仍必须保留 precise `BattlefieldObjectId`。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/HostileTakeoverGuardTests.cs`
- 或新增窄域测试文件：`tests/Riftbound.ConformanceTests/BattlefieldControlLifecycleTests.cs`

必要时可只读：

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`

禁止并行写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- battle lifecycle tests
- PaymentEngine tests
- frontend files
- card coverage matrix

## 4. Implementation Notes

Expected narrow fix shape:

- Preserve precise `BattlefieldObjectId` for a battlefield object whose controller/player zone changes but whose existing object location already points to a concrete battlefield.
- Do not invent battlefield ids for legacy states without a prior precise location.
- Let existing `AdvancePendingBattlefieldTasksAfterStateChange` consume the now-correct `BattlefieldStates` and open the scoped `START_SPELL_DUEL` / `START_BATTLE` tasks.
- Keep state mutations transactional: invalid target / stale target / wrong controller remains rejected or no-effect without changing authoritative hash.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~BattlefieldControlLifecycle|FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~PendingTaskQueue"
```

Focused acceptance should include at least:

- Hostile Takeover control-change preserves target `ObjectLocations[target].BattlefieldObjectId`.
- Same concrete battlefield with remaining opponent unit becomes contested and starts pending spell-duel / battle tasks.
- Optional: no contest starts when no opposing occupant remains, while precise battlefield location is still preserved.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~MoveUnit|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~ObjectLocation"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 7. No-Go

- Do not rewrite Hostile Takeover full official behavior.
- Do not close Hostile Takeover full-official coverage, P0-002, P0-003, P0-004, P0-005, P1, or READY.
- Do not change frontend fallback behavior.
- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not start PaymentEngine or LayerEngine work.
- Do not mark active goal complete.
