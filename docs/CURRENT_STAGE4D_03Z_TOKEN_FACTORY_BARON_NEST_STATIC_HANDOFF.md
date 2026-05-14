# Stage 4D-03Z Token Factory Baron Nest Static Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03Z 的 B 侧服务端实现交接范围。A 主控只记录当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-002、P0-003、P0-004、P0-005、P1 或项目 **NOT READY** 结论。

## 1. 目标

4D-03Z 是一个 focused token battlefield static slice：实现官方 `UNL·T01` 男爵巢穴 token battlefield 的静态移动文本，并从 `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 中只退役该一个 surface。

目标 surface：

- `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`
- source card no: `UNL·T01`
- official anchor: `单位可从任意位置移动到此处`
- surface kind: `battlefield-static`

本切片只实现 destination-specific movement permission：当服务端确认目的地是当前玩家控制 / legacy-owned 的 `UNL·T01` battlefield object 时，当前玩家的受控正面非战斗单位可从另一处精确战场移动到该男爵巢穴，且不需要 `ROAM` optional cost。基地到具体战场的移动已有 `BASE -> BATTLEFIELD:<objectId>` 路径，本切片需要确保 Baron Nest 作为 concrete battlefield destination 继续走该权威路径。

## 2. 当前代码事实

- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs` 已定义 `UNL·T01` 男爵巢穴为 official battlefield token identity，但 `GetDeferredRuleSurfaces()` 仍保留 `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`。
- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 当前有 3 项：Image copy、Brush battlefield replacement、Baron Nest movement static。
- `CoreRuleEngine` 已有精确战场移动基础设施：
  - `ResolveBaseToPreciseBattlefieldMoveUnit` 支持 `BASE -> BATTLEFIELD:<known battlefield object>`。
  - `ResolvePreciseRoamMoveUnit` 支持带 `ROAM` optional cost 的 `BATTLEFIELD:<origin> -> BATTLEFIELD:<destination>`。
  - `ObjectLocations` / `BattlefieldStates` / task queue 已可承载目的地 battlefield object id。
- `MatchSession.MoveUnitSourceRequirements` 已给 `ROAM` path 生成 server-authored prompt metadata，但没有 `UNL·T01` destination-specific no-ROAM battlefield-to-battlefield path。
- 已有相邻代表测试：
  - `P79BattlefieldStaticRoamAllowsPreciseBattlefieldMovement`
  - `P79BattlefieldStaticRoamPromptSkipsOpponentControlledSource`
  - `P4MoveUnitCommandMovesBaseUnitToConcreteBattlefield`
  - `PreciseRoamPreservesDestinationCasingAndQueuesOnlyDestinationContestTasks`
  - `P79BattlefieldStaticRoamSeedAllowsPreciseBattlefieldMove`

## 3. 建议实现口径

- 在移动规则中新增 Baron Nest destination-specific permission，而不是把 `UNL·T01` 当作普通 `ROAM` provider。
- 建议新增 helper：
  - `BaronNestTokenCardNo = "UNL·T01"`
  - `IsBaronNestBattlefieldCardNo`
  - `CanMoveToBaronNestBattlefield` / `TryResolveBaronNestMoveUnit`
- command path：
  - 接受 `MOVE_UNIT`，`origin = BATTLEFIELD:<originBattlefieldObjectId>`，`destination = BATTLEFIELD:<baronNestObjectId>`，`optionalCosts = []`。
  - 要求 source 是当前玩家控制 / legacy-owned 的正面单位、已有 cardNo、非 attacking / defending、精确 origin 与 `ObjectLocations` 一致。
  - 要求 destination battlefield object 存在、cardNo 为 `UNL·T01`、是 battlefield card object、由当前玩家控制 / legacy-owned，且不是同一 origin。
  - 成功后更新 source `ObjectLocations` 到 Baron Nest，保留 `PlayerZones[player].Battlefields` 成员关系，触发既有 cleanup / contest task advancement。
  - 事件仍可使用 `UNIT_MOVED_TO_BATTLEFIELD`，但 payload 应有可审计标记，例如 `movementPermission = "BARON_NEST_MOVE_STATIC"`；不要写 `movementKeyword = "游走"`。
- prompt path：
  - 对位于其他精确战场的合法单位，若当前玩家控制 / legacy-owned `UNL·T01` battlefield object，`MOVE_UNIT` metadata 中应出现一个不带 optional cost 的 destination choice 指向该 Baron Nest。
  - 不应在对手控制 / dirty-owned Baron Nest 时暴露该 path。
  - 不应给没有 cardNo、face-down、combatant、opponent-controlled source 暴露该 path。
- catalog path：
  - `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 从 3 降为 2，只保留 Image copy 和 Brush replacement。
  - 新增 implemented / retired representative entry 用于审计 Baron Nest surface id、官方文本 anchor、surface kind、target count、non-activated flag，以及 `UNL·T01` token identity。
  - 不要把 Image / Brush 一并退役。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`
- 如需 Hub seed smoke，可写 `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` 与 dev seed builder。
- 本切片完成后的 audit / evidence docs 和顶层状态文档。

不建议写入：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 未跟踪文件 `riftbound-dotnet.sln`。
- 与 Baron Nest movement static 无关的 PaymentEngine / LayerEngine / copy-token / Brush replacement 逻辑。

## 5. 必补测试

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 只剩 Image 和 Brush；Baron Nest 不再 deferred。
- 新 implemented / retired token rule surface list 包含 `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`，保留官方文本 anchor、`battlefield-static` kind、0 targets、non-activated command surface。
- `MOVE_UNIT` 可在 no-ROAM optional cost 下把受控正面非战斗单位从 `BATTLEFIELD:<other>` 移到受控 `BATTLEFIELD:<UNL·T01>`。
- 成功事件不宣称 `movementKeyword = "游走"`，并含 Baron Nest static audit marker。
- 手写命令在以下情况 rejected no-mutation：
  - destination 不是 `UNL·T01` 且 source 没有 Roam；
  - destination Baron Nest 由对手控制 / dirty-controlled；
  - origin 与 `ObjectLocations` 不一致；
  - source 无 cardNo、face-down、attacking/defending、opponent-controlled。
- prompt metadata 对合法 source 暴露 Baron Nest destination path，且不要求 `ROAM` optional cost。
- 既有 `ROAM` path、base-to-concrete battlefield path、battlefield static roam path 和 task queue regression 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~BaronNest|FullyQualifiedName~P79BattlefieldStaticRoam|FullyQualifiedName~P4MoveUnitCommand|FullyQualifiedName~P5MoveUnitCommand|FullyQualifiedName~PreciseRoam|FullyQualifiedName~BoardTaskQueue"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoveUnit|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~DeclareBattle"
```

若改动影响 prompt / movement / catalog global counts，A 验收时追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要把 `UNL·T01` 当作普通 `ROAM` provider。
- 不要让 Baron Nest 允许移动到任意战场；它只允许移动到 Baron Nest 自身。
- 不要退役 Image copy-token 或 Brush battlefield replacement deferred surfaces。
- 不要修改 coverage matrix full-official 状态。
- 不要声明 token factory / battlefield / movement / LayerEngine full official。
- 不要关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或 active goal。

## 8. A 侧结论

4D-03Z 用来把 `UNL·T01` 男爵巢穴 token battlefield static 从 P6 deferred list 推进为可执行的服务端移动代表路径。它是 focused movement/token slice，不是 READY 切片；项目仍 **NOT READY**。
