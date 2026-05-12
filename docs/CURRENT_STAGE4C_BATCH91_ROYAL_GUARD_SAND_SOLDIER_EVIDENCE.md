# Stage 4C-91 Royal Guard Sand Soldier Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·157/221` registered as direct card behavior
  - cost `4`
  - `EffectKind: ROYAL_GUARD_PLAY_UNIT_CREATE_SAND_SOLDIER`
  - `TargetCount: 0`
  - `CreatedBaseUnitTokenCount: 1`
  - `CreatedBaseUnitTokenPower: 2`
  - `CreatedBaseUnitTokenName: 黄沙士兵`
  - `CreatedBaseUnitTokenTags: CARD_TYPE:UNIT|黄沙士兵`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 2`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-royal-guard-create-sand-soldier.fixture.json`
  - records ordinary hand play / pay 4 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks `UNIT_TOKEN_CREATED`
  - locks final P1 base containing source unit and token
  - locks source unit 2 power, `CARD_TYPE:UNIT`, active state, empty graveyard, empty stack, and spent mana pool
  - locks Sand Soldier token 2 power, `CARD_TYPE:UNIT|黄沙士兵`, active state, and source-linked token id
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysRoyalGuardCreateSandSoldier`
  - `CoreRuleEngineRejectsRoyalGuardWhenTargetsAreProvided`
  - adjacent source-unit, token, play-card, target, stack, priority, and payment tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalGuard|FullyQualifiedName~SandSoldier|FullyQualifiedName~CreateSandSoldier"
```

结果：10/10 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CreateSandSoldier|FullyQualifiedName~TokenCreated|FullyQualifiedName~UnitToken|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"
```

结果：1880/1880 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch91RoyalGuardSandSoldierEvidence`
  - `functionalUnits[].stage4C91`
  - `snapshotEntries[].stage4C91`

4B freeze status 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。本批只入账 Royal Guard / 皇家守卫 representative source-unit + Sand Soldier token creation route 与 target rejection，不声明精确“此处”目的地、full-official 或 final READY。
