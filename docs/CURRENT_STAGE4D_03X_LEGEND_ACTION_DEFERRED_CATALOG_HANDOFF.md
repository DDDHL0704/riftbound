# Stage 4D-03X Legend Action Deferred Catalog Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03X 的 B 侧服务端实现交接范围。A 主控只记录当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005、P1 或项目 **NOT READY** 结论。

## 1. 目标

4D-03X 是一个 focused catalog hygiene / regression slice：退役 `P6LegendAbilityCatalog` 中已经被 `LEGEND_ACT` 领域实现的五个旧 deferred legend activated-ability representative，避免当前审计继续误报“legend activated ability deferred”。

需要处理的五个旧 surface：

- `FND-259/298` Yasuo：`LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT`
- `OGN·257/298` Lee Sin：`LEGEND_PAY_1_EXHAUST_GRANT_BOON`
- `UNL-234/219` Diana：`LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA`
- `UNL-237/219` Poppy：`LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW`
- `FND-265/298` Viktor：`LEGEND_PAY_1_EXHAUST_CREATE_MINION`

本切片只修正 catalog / tests / docs 的代表性状态：这些能力已经不是 manual deferred，但它们仍必须通过 `LEGEND_ACT` 命令路径执行，不能进入 `ACTIVATE_ABILITY` / `P4ActivatedAbilityCatalog`。

## 2. 当前代码事实

- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 已将上述五张及其 functional reprints 纳入 `IsImplementedLegendActionCard`，实现域为 `LEGEND_ACTION_DOMAIN`。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 的 `P6LegendRuleDomainSurfacesReportManualBoundaryCoverage` 当前断言：legend specs `106/106` implemented，manual legend specs `0`。
- `CoreRuleEngine` 和 `MatchSession` 已实现对应 `LEGEND_ACT` ability：
  - Yasuo move friendly unit；
  - Lee Sin grant boon；
  - Diana spell-duel focus gain 1 mana；
  - Poppy spend 3 experience draw；
  - Viktor pay 1 create minion。
- 现有回归已覆盖这些 `LEGEND_ACT` positive / guard paths：
  - `P79LegendActSpendsExperienceExhaustsLegendAndDraws`
  - `P79LegendActMovesFriendlyUnitWithYasuo`
  - `P79LegendActGrantsBoonWithLeeSin`
  - `P79LegendActCreatesMinionWithViktor`
  - `P79LegendActDianaGainsManaDuringSpellDuelFocus`
- 但 `src/Riftbound.Engine/P6LegendAbilityCatalog.cs` 仍以 `P6DeferredLegendAbilitySurface` / `GetDeferredSurfaces()` 返回这五个旧 surface，且测试仍断言 count = 5。这与当前 `LEGEND_ACTION_DOMAIN` implemented 状态不一致。

## 3. 建议实现口径

- 将 `P6LegendAbilityCatalog` 的当前五项从 “deferred surfaces” 语义迁移到 “retired / implemented legend action representative surfaces” 语义。
- 建议保留 `GetDeferredSurfaces()` 兼容入口但返回空集合，明确 legend activated representatives 不再计入 P6 manual deferred catalog。
- 新增或重命名代表性入口，例如 `GetRetiredDeferredSurfaces()` / `GetImplementedLegendActionSurfaces()`，用于测试这些旧 surface：
  - 官方文本 anchor 仍匹配；
  - `BehaviorSpecCatalog` 状态为 `Implemented`；
  - `ImplementedEffectKind == LEGEND_ACTION_DOMAIN`；
  - 不在 `P4ActivatedAbilityCatalog`；
  - 不在 direct `CardBehaviorRegistry`；
  - 手写 `ACTIVATE_ABILITY` 仍以中文拒绝且 no-mutation。
- 更新测试命名：不要再称这些路径为 `DeferredLegendAbilitySurface`，改成 “retired deferred / implemented legend action representative / activate-ability blocked surface”。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/P6LegendAbilityCatalog.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 本切片完成后的 audit / evidence docs 和顶层状态文档。

不建议写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 未跟踪文件 `riftbound-dotnet.sln`。

## 5. 必补测试

- `P6LegendAbilityCatalog.GetDeferredSurfaces()` 为 empty，或者等价地证明五个旧 representative 不再属于 deferred list。
- 新的 retired / implemented representative list 包含五个旧 surface，并审计官方文本、`LEGEND_ACTION_DOMAIN` BehaviorSpec、非 P4 registry、非 direct CardBehaviorRegistry。
- 旧 surface 用 `ACTIVATE_ABILITY` 提交仍拒绝并保持 no-mutation，错误消息不含 raw `ACTIVATE_ABILITY`。
- 现有五条 positive `LEGEND_ACT` representative 继续通过。
- `P6LegendRuleDomainSurfacesReportManualBoundaryCoverage` 继续证明 legend specs `106/106` implemented、manual legend specs `0`。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6LegendAbilityCatalog|FullyQualifiedName~P6LegendRuleDomainSurfacesReportManualBoundaryCoverage|FullyQualifiedName~P79LegendAct|FullyQualifiedName~LegendAction"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendAct|FullyQualifiedName~LegendAction|FullyQualifiedName~Diana|FullyQualifiedName~Yasuo|FullyQualifiedName~LeeSin|FullyQualifiedName~Poppy|FullyQualifiedName~Viktor|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngine|FullyQualifiedName~RunePool"
```

若改动影响 BehaviorSpec / catalog global counts，A 验收时追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要改 `LEGEND_ACT` 运行时行为；本切片只退役旧 catalog deferred 语义。
- 不要把 legend action 加进 `P4ActivatedAbilityCatalog` 或 `ACTIVATE_ABILITY`。
- 不要修改 coverage matrix full-official 状态。
- 不要声明全部 legend / battlefield / token rule domains full official。
- 不要关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03X 用来把 P6 旧 deferred catalog 与当前 `LEGEND_ACTION_DOMAIN` implemented 事实对齐。它是审计卫生和回归护栏切片，不是 READY 切片；项目仍 **NOT READY**。
