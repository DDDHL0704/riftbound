# Stage 4C-79 Forced Conscription Control Small Enemy Recall Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-140/219` registered as direct card behavior
  - cost `5`
  - `TargetCount: 1`
  - `TargetScope: EnemyBattlefieldUnit`
  - `MaxTargetPower: 3`
  - `TargetRequiredTag: CARD_TYPE:UNIT`
  - `GainsControlOfTargetToBase: true`
  - `ExhaustsControlledTarget: true`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-forced-conscription-control-small-enemy-recall.fixture.json`
  - records ordinary hand play / pay 5 / one enemy battlefield unit target / pass-pass resolution
  - locks `UNIT_CONTROL_GAINED`
  - locks final P1 base containing the controlled target
  - locks target exhausted after control gain and recall
  - explicitly defers the optional 5 experience branch and complete owner/controller model

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysForcedConscriptionControlSmallEnemyRecall`
  - `CoreRuleEngineRejectsForcedConscriptionWhenTargetPowerAboveThree`
  - `CoreRuleEngineForcedConscriptionResolutionSkipsAlreadyControlledEnemyZoneTarget`
  - adjacent control-to-base and control-on-battlefield tests for Taken For A Ride and Hostile Takeover

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ForcedConscription|FullyQualifiedName~TakenForARide|FullyQualifiedName~HostileTakeover"
```

结果：18/18 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Control|FullyQualifiedName~Battlefield|FullyQualifiedName~MoveUnit|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"
```

结果：1718/1718 passed。

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
  - `stage4CBatch79ForcedConscriptionControlSmallEnemyRecallEvidence`
  - `functionalUnits[].stage4C79`
  - `snapshotEntries[].stage4C79`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Forced Conscription representative control-small-enemy-and-recall route, power-above-three rejection, and dirty already-controlled enemy-zone target guard，不清零 optional experience branch、complete owner/controller model、control-zone movement matrix、PaymentEngine optional costs、complete FEPR、hidden-info 或 full-official 缺口。
