# Stage 4D-03Z Token Factory Baron Nest Static Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03Z 完成 `UNL·T01` 男爵巢穴 token battlefield static 的 focused representative：服务端现在支持受控正面非战斗单位从另一处精确战场 no-ROAM 移动到受控 Baron Nest。

## Scope

已退役的 token deferred representative：

- `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`
- source card no: `UNL·T01`
- official anchor: `单位可从任意位置移动到此处`
- surface kind: `battlefield-static`

`P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 现在只保留 Image copy-token 与 Brush battlefield replacement 两项。新增 `GetImplementedRuleSurfaces()` 保存 Baron Nest retired representative，用于测试旧 surface id、官方文本锚点、surface kind、activated flag、target count 与 token identity。

## Runtime Boundary

本切片新增 destination-specific movement permission，而不是普通 `ROAM` provider：

- `MOVE_UNIT` 接受 `BATTLEFIELD:<origin>` -> `BATTLEFIELD:<controlled UNL·T01 battlefield object>`，`optionalCosts=[]`。
- 成功事件仍为 `UNIT_MOVED_TO_BATTLEFIELD`，并写入 `movementPermission = "BARON_NEST_MOVE_STATIC"`。
- 成功事件不写 `movementKeyword = "游走"`，以免把 Baron Nest static 误当成 Roam。
- Prompt metadata 新增 `BARON_NEST_MOVE_STATIC` source requirement，destination choices 指向合法 Baron Nest，`optionalCostChoices` 与 `requiredOptionalCosts` 为空，`requiresRoamOptionalCost=false`。

## Guards

已覆盖 rejected no-mutation：

- destination 不是 Baron Nest 且 source 无 Roam；
- destination Baron Nest 由对手控制；
- origin 与 authoritative `ObjectLocations` 不一致；
- source 无 cardNo；
- source face-down；
- source attacking / defending；
- source 由对手控制。

## No-Go

- 未把 `UNL·T01` 当作普通 `ROAM` provider。
- 未退役 Image copy-token 或 Brush battlefield replacement deferred surfaces。
- 未修改前端运行时代码。
- 未修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或任何 full-official 项目状态。

## Residual Risk

本切片只实现 Baron Nest focused movement representative。Image copy-token、Brush battlefield replacement、完整 token factory / movement / battlefield full-official 仍未关闭；项目仍 **NOT READY**。
