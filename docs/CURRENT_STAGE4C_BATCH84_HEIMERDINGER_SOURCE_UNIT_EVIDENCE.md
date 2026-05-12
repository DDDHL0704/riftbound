# Stage 4C-84 Heimerdinger Source Unit Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `ARC-003/006` registered as direct card behavior
  - cost `3`
  - `EffectKind: ARC_HEIMERDINGER_YORDLE_STATIC_PLAY_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 3`
  - `SourceUnitTags: 约德尔人`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·111/298` registered as direct card behavior
  - cost `3`
  - `EffectKind: OGN_HEIMERDINGER_YORDLE_TAP_STATIC_PLAY_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 3`
  - `SourceUnitTags: 约德尔人`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-arc-heimerdinger-yordle-static.fixture.json`
  - records ordinary hand play / pay 3 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the ARC source object
  - locks 3 power, `CARD_TYPE:UNIT|约德尔人`, active state, empty stack, empty hand, and spent mana pool
  - explicitly defers copied tap skills
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-heimerdinger-yordle-static.fixture.json`
  - records ordinary hand play / pay 3 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the OGN source object
  - locks 3 power, `CARD_TYPE:UNIT|约德尔人`, active state, empty stack, empty hand, and spent mana pool
  - explicitly defers copied tap skills

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysActiveEntrySourceUnit`
  - `CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided`
  - `CoreRuleEnginePlaysKeywordOnlySourceUnit`
  - `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided`
  - `OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub`
  - adjacent source-unit, target, stack, priority, payment, and activated-ability tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysActiveEntrySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided|FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided|FullyQualifiedName~OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub"
```

结果：484/484 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Heimerdinger|FullyQualifiedName~Yordle|FullyQualifiedName~ActiveEntrySourceUnit|FullyQualifiedName~KeywordOnlySourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~Tap|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~OfficialDeck"
```

结果：1847/1847 passed。

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
  - `stage4CBatch84HeimerdingerSourceUnitEvidence`
  - `functionalUnits[].stage4C84`
  - `snapshotEntries[].stage4C84` for both `ARC-003/006` and `OGN·111/298`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 shared Heimerdinger representative ordinary hand source-unit-to-base route、target rejection、official opening candidate visibility，不清零 copied tap skills、static ability-copy model、FAQ review、PaymentEngine、FEPR、hidden-info 或 full-official 缺口。
