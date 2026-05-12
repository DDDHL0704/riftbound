# Stage 4C-89 Vanilla Unit Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·142/298` registered as direct card behavior
  - cost `9`
  - `EffectKind: MOUNTAIN_DRAKE_PLAY_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 10`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·175/298` registered as direct card behavior
  - cost `3`
  - `EffectKind: DOCKSIDE_LURKER_PLAY_UNIT`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 3`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mountain-drake-vanilla-unit.fixture.json`
  - records ordinary hand play / pay 9 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the source object
  - locks 10 power, `CARD_TYPE:UNIT`, empty stack, and spent mana pool
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-dockside-lurker-vanilla-unit.fixture.json`
  - records ordinary hand play / pay 3 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks final P1 base containing the source object
  - locks 3 power, `CARD_TYPE:UNIT`, empty stack, and spent mana pool
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysVanillaSourceUnit`
  - `CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided`
  - adjacent source-unit, play-card, target, stack, priority, and payment tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided"
```

结果：305/305 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~VanillaSourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"
```

结果：1879/1879 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch89VanillaUnitEvidence`
  - `functionalUnits[].stage4C89`
  - `snapshotEntries[].stage4C89`

4B freeze status 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。本批只入账 Mountain Drake / Dockside Lurker representative vanilla source-unit route 与 target rejection，不声明 full-official 或 final READY。
