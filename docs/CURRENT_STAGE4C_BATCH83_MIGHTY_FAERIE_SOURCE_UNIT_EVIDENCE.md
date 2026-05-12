# Stage 4C-83 Mighty Faerie Source Unit Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·125/221` registered as direct card behavior
  - cost `4`
  - `EffectKind: MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 4`
  - `SourceUnitTags: 仙灵`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mighty-faerie-move-payment-static.fixture.json`
  - records ordinary hand play / pay 4 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the source object
  - locks 4 power, `CARD_TYPE:UNIT|仙灵`, active state, empty stack, empty hand, and spent mana pool
  - explicitly defers movement trigger, optional purple payment, and same-battlefield movement
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-mighty-faerie-target-rejected.fixture.json`
  - locks explicit-target rejection for the current zero-target ordinary hand play path
  - locks no tick, no events, no cost, no stack, no zone mutation, and unchanged target body

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysKeywordOnlySourceUnit`
  - `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided`
  - `P4MightyFaerieTargetRejectedFixture`
  - adjacent keyword/source-unit, battlefield, movement, target, stack, priority, and payment tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided|FullyQualifiedName~P4MightyFaerieTargetRejectedFixture"
```

结果：460/460 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MightyFaerie|FullyQualifiedName~Faerie|FullyQualifiedName~KeywordOnlySourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~MoveUnit|FullyQualifiedName~Battlefield|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority"
```

结果：2117/2117 passed。

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
  - `stage4CBatch83MightyFaerieSourceUnitEvidence`
  - `functionalUnits[].stage4C83`
  - `snapshotEntries[].stage4C83`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Mighty Faerie representative ordinary hand source-unit-to-base route 与 explicit-target rejection，不清零 move-to-battlefield trigger、optional purple power payment、same-battlefield friendly-unit movement、PaymentEngine、FEPR、control-zone movement、hidden-info 或 full-official 缺口。
