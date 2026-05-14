# Stage 4D-01B Control Change Precise Battlefield Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片验证 control-change-to-battlefield 后，服务端继续保留对象所在的 precise battlefield identity，并让既有 battlefield task advancement 使用该 authoritative location 派生 contest / spell-duel / battle tasks。

覆盖代表：

- Hostile Takeover / `SFD·202/221`
- P2 battlefield `BF-CONTROL`
- 被夺取目标从 P2 battlefield zone 移入 P1 battlefield zone
- 同一 concrete battlefield 上可选保留 P2 occupant

## Implemented Behavior

- `ReconcileObjectLocations` 在对象仍处于 battlefield zone 时保留既有 `BattlefieldObjectId`，不再要求 reconciled player id 与旧 location player id 相同。
- Hostile Takeover pass-pass 结算后，被夺取单位 owner 仍为 P2、controller 变为 P1、zone 仍为 `BATTLEFIELD`。
- 被夺取单位 `ObjectLocations[target].BattlefieldObjectId` 保持为 `BF-CONTROL`。
- 当 `BF-CONTROL` 上仍有 P2 occupant 时，服务端 authoritative `BattlefieldStates["BF-CONTROL"]` 标记 contested，occupant controllers 为 P1 / P2。
- Pending queue 进入 `SPELL_DUEL_TASKS`，active task 为 `task:start-spell-duel:BF-CONTROL`，tasks 包含 `BATTLEFIELD_CONTESTED`、`START_SPELL_DUEL`、`START_BATTLE`。
- Events 包含 `BATTLEFIELD_CONTESTED` 与 `SPELL_DUEL_STARTED`，并携带同一个 battlefield object id / task id。
- 当没有敌方 occupant 留在该 concrete battlefield 时，不会凭 battlefield object controller 误开 contest，但仍保留 precise battlefield identity。

## Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/HostileTakeoverGuardTests.cs`

## Remaining Scope

本切片只收口 control-change precise battlefield preservation 的 representative guard。它不关闭完整 standby / control / conquer / held lifecycle，不关闭 P0-002、P0-003、P0-004、P0-005、P1、前端最终验收、card matrix 或 READY。
