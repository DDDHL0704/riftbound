# Stage 4D-03Y Battlefield Deferred Catalog Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03Y 的 B 侧服务端实现交接范围。A 主控只记录当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-002、P0-003、P0-004、P0-005、P1 或项目 **NOT READY** 结论。

## 1. 目标

4D-03Y 是一个 focused catalog hygiene / regression slice：退役 `P6BattlefieldEffectCatalog` 中已经被 `BATTLEFIELD_RULE_DOMAIN` 领域实现的四个旧 deferred battlefield representative，避免当前审计继续误报“battlefield effects deferred”。

需要处理的四个旧 surface：

- `SFD·208/221` Poro Forge：`BATTLEFIELD_DEFERRED_GRANT_LEGEND_EXHAUST_ATTACH_WEAPON`
- `OGN·292/298` Dream Tree：`BATTLEFIELD_DEFERRED_FIRST_FRIENDLY_SPELL_DRAW`
- `UNL-206/219` Blood Altar：`BATTLEFIELD_DEFERRED_DESTROYED_IN_BATTLE_REPLACEMENT_RECALL`
- `UNL-208/219` Blackflame Altar：`BATTLEFIELD_DEFERRED_STATIC_KEYWORD_GRANT_EPHEMERAL_DEFENDER_BONUS`

本切片只修正 catalog / tests / docs 的代表性状态：这些战场规则已经不是 manual deferred，但它们仍必须通过战场规则域事件、`DECLARE_BATTLE` / combat lifecycle / server-authored prompt 执行，不能进入 `ACTIVATE_ABILITY` / `P4ActivatedAbilityCatalog`。

## 2. 当前代码事实

- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 已将上述四张卡纳入 `IsImplementedBattlefieldRuleCard`，实现域为 `BATTLEFIELD_RULE_DOMAIN`。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 的 `P6BattlefieldRuleDomainSurfacesReportManualBoundaryCoverage` 当前断言：battlefield specs `57/57` implemented，manual battlefield specs 为 `0`。
- `CoreRuleEngine` / `MatchSession` 已存在这些代表路径：
  - Poro Forge 授予友方传奇贴附受控武装的 legend action；
  - Dream Tree 每回合首次友方单位被己方法术指定后抽牌；
  - Blood Altar 战斗中将被摧毁单位可支付 3 mana 改为召回基地；
  - Blackflame Altar 给此处瞬息单位防守时的坚守 / battle bonus。
- 现有回归已覆盖这些 battlefield positive / guard paths：
  - `P79BattlefieldForgeGrantsLegendArmamentAttach`
  - `P79BattlefieldForgeReattachesControlledArmament`
  - `P79BattlefieldFriendlySpellTargetDrawsOnce`
  - `P79BattlefieldFriendlySpellTargetDoesNotDrawTwiceThisTurn`
  - `P79BattlefieldFriendlySpellTargetSkipsOpponentControlledSource`
  - `P79BattlefieldBattleDestroyedUnitPaysThreeAndRecalls`
  - `P79BattlefieldBattleDestroyedUnitSkipsOpponentOwnedAltar`
  - `P79BattlefieldBattleDestroyedUnitFallsBackToDestroyWhenNoMana`
  - `P79BattlefieldEphemeralDefenderGainsSteadfast`
- 但 `src/Riftbound.Engine/P6BattlefieldEffectCatalog.cs` 仍以 `P6DeferredBattlefieldEffectSurface` / `GetDeferredSurfaces()` 返回这四个旧 surface，且 completion audit test 仍断言 count = 4。这与当前 `BATTLEFIELD_RULE_DOMAIN` implemented 状态不一致。

## 3. 建议实现口径

- 将 `P6BattlefieldEffectCatalog` 的当前四项从 “deferred surfaces” 语义迁移到 “retired / implemented battlefield rule representative surfaces” 语义。
- 建议保留 `GetDeferredSurfaces()` 兼容入口但返回空集合，明确这些 battlefield representatives 不再计入 P6 manual deferred catalog。
- 新增或重命名代表性入口，例如 `GetImplementedBattlefieldRuleSurfaces()`，用于测试这些旧 surface：
  - 官方文本 anchor 仍匹配；
  - `BehaviorSpecCatalog` 状态为 `Implemented`；
  - `ImplementedEffectKind == BATTLEFIELD_RULE_DOMAIN`；
  - 不在 `P4ActivatedAbilityCatalog`；
  - 不在 direct `CardBehaviorRegistry`；
  - activated grant representative 的手写 `ACTIVATE_ABILITY` 仍以中文拒绝且 no-mutation。
- 更新测试命名：不要再称这些路径为 `DeferredBattlefieldEffectSurface`，改成 “retired deferred / implemented battlefield rule representative / activate-ability blocked surface”。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/P6BattlefieldEffectCatalog.cs`
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

- `P6BattlefieldEffectCatalog.GetDeferredSurfaces()` 为 empty，或者等价地证明四个旧 representative 不再属于 deferred list。
- 新的 retired / implemented representative list 包含四个旧 surface，并审计官方文本、`BATTLEFIELD_RULE_DOMAIN` BehaviorSpec、非 P4 registry、非 direct CardBehaviorRegistry。
- `SFD·208/221` 的 battlefield-granted activated representative 用 `ACTIVATE_ABILITY` 提交仍拒绝并保持 no-mutation，错误消息不含 raw `ACTIVATE_ABILITY`。
- 现有四类 positive battlefield representative 继续通过。
- `P6BattlefieldRuleDomainSurfacesReportManualBoundaryCoverage` 继续证明 battlefield specs `57/57` implemented、manual battlefield specs `0`。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldEffectCatalog|FullyQualifiedName~P6BattlefieldRuleDomain|FullyQualifiedName~P79BattlefieldForge|FullyQualifiedName~P79BattlefieldFriendlySpellTarget|FullyQualifiedName~P79BattlefieldEphemeralDefender|FullyQualifiedName~P79BattlefieldBattleDestroyed"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngine|FullyQualifiedName~RunePool|FullyQualifiedName~DeclareBattle"
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

- 不要改 battlefield / combat / legend action 运行时行为；本切片只退役旧 catalog deferred 语义。
- 不要把 battlefield-granted action 加进 `P4ActivatedAbilityCatalog` 或 `ACTIVATE_ABILITY`。
- 不要修改 coverage matrix full-official 状态。
- 不要声明全部 battlefield / legend / token rule domains full official。
- 不要关闭 P0-002、P0-003、P0-004、P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03Y 用来把 P6 旧 deferred catalog 与当前 `BATTLEFIELD_RULE_DOMAIN` implemented 事实对齐。它是审计卫生和回归护栏切片，不是 READY 切片；项目仍 **NOT READY**。
