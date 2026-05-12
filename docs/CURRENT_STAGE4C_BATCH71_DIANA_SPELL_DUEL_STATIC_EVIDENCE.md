# Stage 4C-71 Diana Spell Duel Static Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-079/219` / `DIANA_SPELL_DUEL_INSIGHT_STATIC`
  - `Cost: 3`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 3`
  - `SourceUnitTags: 巨神峰`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - keyword/static source-unit 普通手牌打出路径在 stack pass-pass 后把源牌创建为控制者基地单位。
  - 当前代表路线为 0 目标；unexpected target 在支付、入栈、手牌移动和单位创建前被拒绝。
  - Diana 卡面中的法术对决 Insight 触发、支付、展示和抽取路径仍 deferred。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-diana-spell-duel-static.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 3 / zero-target stack / pass-pass resolution
  - source enters base as 3-power `CARD_TYPE:UNIT|巨神峰` unit
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysKeywordOnlySourceUnit`
  - `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided"
```

结果：459/459 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Diana|FullyQualifiedName~SpellDuel|FullyQualifiedName~Insight|FullyQualifiedName~PassFocus|FullyQualifiedName~Swift"
```

结果：45/45 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3754/3754 passed。

```sh
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

结果：frontend build passed；Chrome smoke passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch71DianaSpellDuelStaticEvidence`
  - `functionalUnits[].stage4C71`
  - `snapshotEntries[].stage4C71`

4B freeze status / status flags 保持不变，`fullOfficial=false`。`UNL-079a/219` / `FU-085a7d6c4b` 不在本批贴标范围内。
