# Stage 4C-75 Gentleman Duel Damage Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGS·008/024` registered as direct card behavior
  - cost `6`
  - `TargetCount: 2`
  - `TargetScope: CardTargetScopes.FriendlyThenEnemyUnits`
  - `PowerModifierAmount: 3`
  - `DealsMutualTargetPowerDamage: true`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json`
  - records official catalog evidence and core rules evidence
  - locks power modifier before mutual current-power damage
  - locks lethal enemy-target destruction and owner graveyard placement

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysGentlemanDuelPowerThenMutualDamage`
  - `CoreRuleEnginePlaysDuelMutualPowerDamage`
  - `CoreRuleEngineRejectsDuelWhenTargetsAreReversed`
  - `P4DuelTargetOrderRejectedFixture`
  - `CoreRuleEnginePlaysMarchingOrdersEchoMutualPowerDamage`
  - `CoreRuleEnginePlaysClashOfGiantsMutualPowerDamage`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysGentlemanDuelPowerThenMutualDamage|FullyQualifiedName~CoreRuleEnginePlaysDuelMutualPowerDamage|FullyQualifiedName~CoreRuleEngineRejectsDuelWhenTargetsAreReversed|FullyQualifiedName~P4DuelTargetOrderRejectedFixture|FullyQualifiedName~CoreRuleEnginePlaysMarchingOrdersEchoMutualPowerDamage|FullyQualifiedName~CoreRuleEnginePlaysClashOfGiantsMutualPowerDamage"
```

结果：6/6 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gentleman|FullyQualifiedName~Duel|FullyQualifiedName~MutualPower|FullyQualifiedName~PowerModified|FullyQualifiedName~Damage|FullyQualifiedName~Cleanup|FullyQualifiedName~ClashOfGiants|FullyQualifiedName~MarchingOrders|FullyQualifiedName~FriendlyThenEnemy"
```

结果：203/203 passed。

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
  - `stage4CBatch75GentlemanDuelDamageEvidence`
  - `functionalUnits[].stage4C75`
  - `snapshotEntries[].stage4C75`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Gentleman Duel representative ordinary stack damage route，不清零 Swift / spell-duel timing、LayerEngine、duration cleanup、replacement / prevention、complete target matrix 或 full-official 缺口。
