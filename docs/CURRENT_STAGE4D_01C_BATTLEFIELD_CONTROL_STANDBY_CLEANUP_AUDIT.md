# Stage 4D-01C Battlefield Control Standby Cleanup Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片验证 natural battle close / battlefield control resolve 后，由战场控制权变化导致的 illegal standby cleanup 会在后续 contested battlefield task advancement 前完成，并且隐藏 standby identity 不会通过非授权 snapshot / prompt 泄漏。

覆盖代表：

- 当前 battle：`BF-DAMAGE`
- 后续 contested battlefield：`BF-NEXT`
- P2 face-down standby：`P2-HIDDEN-STANDBY`
- battle close 后 `BF-DAMAGE` controller 从 P2 变为 P1

## Implemented Behavior

- `ASSIGN_COMBAT_DAMAGE` 完成当前 natural battle 后，先输出 `BATTLEFIELD_CONTROL_RESOLVED`。
- 控制权从 P2 变为 P1 后，`BF-DAMAGE` 上 P2 face-down standby 被判定为 illegal standby。
- 服务端随后输出 `BATTLEFIELD_STANDBY_REMOVED`，`reason == BATTLEFIELD_CONTROL_CLEANUP`。
- 被清理 standby 进入 P2 graveyard，`ObjectLocations[standby].Zone == "GRAVEYARD"`，`BattlefieldObjectId == null`，card object 变为 face-up 且 controller 回 owner。
- cleanup 发生在 `BF-NEXT` 的 `BATTLEFIELD_CONTESTED` 与 `SPELL_DUEL_STARTED` 之前。
- 最终不残留当前 `BF-DAMAGE` 的 `START_BATTLE` task、cleanup task、assignment prompt 或 battle declaration prompt。
- `MatchSession` 的 lane snapshot 现在对非授权 viewer 过滤 hidden battlefield standby object id，避免 `lanes.battlefieldObjectIds` 泄漏 face-down standby identity。
- 新 guard 序列化检查 P1 snapshot、spectator snapshot 与 P1 prompt，确认 `P2-HIDDEN-STANDBY` 不泄漏。

## Files

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Remaining Scope

本切片只收口 battle-control-driven standby cleanup ordering 与 lane snapshot hidden standby redaction。它不关闭完整 held / conquer / control lifecycle、full official battle lifecycle、P0-002、P0-003、P0-004、P0-005、P1、前端最终验收、card matrix 或 READY。
