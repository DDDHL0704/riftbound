# Stage 4D-02L Immediate Battle Task Advancement Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片验证 immediate `DECLARE_BATTLE` 直接结算分支在当前 battle close / battlefield control resolution 后，会与 `ASSIGN_COMBAT_DAMAGE` 分支一样推进后续 contested battlefield task，并在 cleanup / payment blocker 存在时保持阻塞。

覆盖代表：

- 当前 battle：`BF-1`
- 后续 contested battlefield：`BF-2`
- 当前 active task：`task:start-battle:BF-1`
- 后续 expected task：`task:start-spell-duel:BF-2`

## Implemented Behavior

- immediate `DECLARE_BATTLE` 完成 `BF-1` battle close 与 `BATTLEFIELD_CONTROL_RESOLVED` 后，复用 `AdvancePendingBattlefieldTasksAfterStateChange(...)`。
- 无 blocker 时，当前 `START_BATTLE:BF-1` 不残留，服务端进入 `SPELL_DUEL_TASKS`，active task 为 `task:start-spell-duel:BF-2`。
- Prompt 变为 `SpellDuelFocus`，related battlefield 为 `BF-2`，不会停留在 stale battle declaration prompt。
- Events 顺序保持当前 battle close / control resolution 先于 `BF-2` 的 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。
- `AdvancePendingBattlefieldTasksAfterStateChange(...)` 新增 `PendingPayment is not null` blocker，避免 conquest / held payment window 未关闭时提前推进其他 battlefield。
- cleanup blocker guard 证明当前 battle 产生 state-based cleanup 时仍停在 `STATE_BASED_CLEANUP`，不会提前启动 `BF-2` spell duel。

## Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`

## Remaining Scope

本切片只收口 immediate battle branch 的 next contested task advancement representative。它不关闭完整 battle lifecycle、完整 held / conquer / control matrix、P0-002、P0-003、P0-004、P0-005、P1、前端最终验收、card matrix 或 READY。
