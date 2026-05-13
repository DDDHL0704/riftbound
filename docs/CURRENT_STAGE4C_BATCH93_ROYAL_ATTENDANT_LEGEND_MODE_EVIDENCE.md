# Stage 4C-93 Royal Attendant Legend Mode Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·039/221` registered as direct card behavior
  - cost `3`
  - `EffectKind: ROYAL_ATTENDANT_LEGEND_READY_PLAY_UNIT`
  - source-unit-to-base route with `SourceUnitPower: 4`
  - `EffectKind: ROYAL_ATTENDANT_READY_LEGEND_PLAY_UNIT`
  - `Mode: READY_LEGEND`
  - `TargetScope: CardTargetScopes.Legend`
  - `ReadiesTarget: true`
  - `EffectKind: ROYAL_ATTENDANT_EXHAUST_LEGEND_PLAY_UNIT`
  - `Mode: EXHAUST_LEGEND`
  - `TargetScope: CardTargetScopes.Legend`
  - `ExhaustsTarget: true`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-royal-attendant-legend-ready-static.fixture.json`
  - records ordinary hand play / pay 3 / legend target / `READY_LEGEND` route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks `UNIT_READIED`
  - locks final P1 base containing the source object
  - locks source unit 4 power, `CARD_TYPE:UNIT`, active state, empty stack, spent mana pool, and target legend `isExhausted=false`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-royal-attendant-target-rejected.fixture.json`
  - records invalid target rejection
  - locks no tick advance, no events, no cost payment, no hand/base movement, and empty stack
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `P79RoyalAttendantReadiesOrExhaustsTargetLegend`
  - `P79RoyalAttendantPromptExposesLegendModesAndTargets`
  - `P4RoyalAttendantTargetRejectedFixture`
  - paired fixture replay for `p2-preflight-play-royal-attendant-legend-ready-static.fixture.json`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - `P79RoyalAttendantSeedOffersLegendModesAndReadiesTarget`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalAttendant|FullyQualifiedName~royal-attendant|FullyQualifiedName~READY_LEGEND|FullyQualifiedName~EXHAUST_LEGEND"
```

结果：5/5 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalAttendant|FullyQualifiedName~Legend|FullyQualifiedName~PlayCardPrompt|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"
```

结果：1894/1894 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch93RoyalAttendantLegendModeEvidence`
  - `functionalUnits[].stage4C93`
  - `snapshotEntries[].stage4C93`

4B freeze status 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。本批只入账 Royal Attendant / 皇家随从 representative source-unit + legend target mode route、prompt / Hub representative evidence 与 invalid target rejection，不声明完整 legend interaction domain、full-official 或 final READY。
