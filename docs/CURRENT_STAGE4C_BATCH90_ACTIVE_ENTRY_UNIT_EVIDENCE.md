# Stage 4C-90 Active Entry Unit Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGS·016/024` registered as direct card behavior
  - cost `6`
  - `EffectKind: VANGUARD_SQUIRE_PLAY_ACTIVE_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 5`
  - `SourceUnitTags: 精锐`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·006/221` registered as direct card behavior
  - cost `3`
  - `EffectKind: AGGRESSIVE_DRAGONHOUND_PLAY_ACTIVE_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 3`
  - `SourceUnitTags: 犬形|龙`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-vanguard-squire-active-unit.fixture.json`
  - records ordinary hand play / pay 6 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the source object
  - locks 5 power, `CARD_TYPE:UNIT|精锐`, active state, empty stack, and spent mana pool
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-aggressive-dragonhound-active-unit.fixture.json`
  - records ordinary hand play / pay 3 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the source object
  - locks 3 power, `CARD_TYPE:UNIT|犬形|龙`, active state, empty stack, and spent mana pool
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysActiveEntrySourceUnit`
  - `CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided`
  - adjacent source-unit, play-card, target, stack, priority, and payment tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysActiveEntrySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided"
```

结果：24/24 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActiveEntry|FullyQualifiedName~ActiveSourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"
```

结果：1879/1879 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch90ActiveEntryUnitEvidence`
  - `functionalUnits[].stage4C90`
  - `snapshotEntries[].stage4C90`

4B freeze status 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。本批只入账 Vanguard Squire / Aggressive Dragonhound representative active-entry source-unit route 与 target rejection，不声明 full-official 或 final READY。
