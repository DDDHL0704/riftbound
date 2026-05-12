# Stage 4C-82 Duel Mutual Power Damage Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·128/298` registered as direct card behavior
  - cost `2`
  - `EffectKind: DUEL_MUTUAL_POWER_DAMAGE`
  - `TargetCount: 2`
  - `TargetScope: FriendlyThenEnemyUnits`
  - `DealsMutualTargetPowerDamage: true`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-duel-mutual-power-damage.fixture.json`
  - records ordinary hand play / pay 2 / friendly then enemy unit targets / stack route
  - locks two `DAMAGE_APPLIED` events with target units as current-power damage sources
  - locks lethal cleanup destroying the enemy target
  - locks friendly unit remaining on battlefield with recorded damage
  - locks source spell to graveyard and empty stack
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-duel-target-order-rejected.fixture.json`
  - locks reversed target-order rejection
  - locks no tick, no events, no cost, no stack, no damage, and no zone mutation

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysDuelMutualPowerDamage`
  - `CoreRuleEngineRejectsDuelWhenTargetsAreReversed`
  - `P4DuelTargetOrderRejectedFixture`
  - mutual-power sibling tests for Gentleman Duel, Marching Orders, and Clash of Giants

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DuelMutualPowerDamage|FullyQualifiedName~RejectsDuelWhenTargetsAreReversed|FullyQualifiedName~DuelTargetOrderRejected"
```

结果：3/3 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Duel|FullyQualifiedName~MutualPower|FullyQualifiedName~FriendlyThenEnemy|FullyQualifiedName~ClashOfGiants|FullyQualifiedName~MarchingOrders|FullyQualifiedName~Gentleman|FullyQualifiedName~PowerModified|FullyQualifiedName~Damage|FullyQualifiedName~Cleanup|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Target"
```

结果：1410/1410 passed。

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
  - `stage4CBatch82DuelMutualPowerDamageEvidence`
  - `functionalUnits[].stage4C82`
  - `snapshotEntries[].stage4C82`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Duel representative friendly-then-enemy mutual current-power damage route、lethal enemy cleanup、reversed target-order rejection，不清零 battle / spell-duel lifecycle、LayerEngine、FEPR、replacement / prevention、hidden-info 或 full-official 缺口。
