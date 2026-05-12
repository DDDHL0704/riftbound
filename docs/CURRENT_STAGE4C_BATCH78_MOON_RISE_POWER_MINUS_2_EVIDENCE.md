# Stage 4C-78 Moon Rise Power Minus 2 Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-198/219` registered as direct card behavior
  - cost `3`
  - `TargetCount: 0`
  - `PowerModifierAmount: -2`
  - `ModifiesAllEnemyBattlefieldUnits: true`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json`
  - records ordinary hand play / pay 3 / zero-target stack / pass-pass resolution
  - locks enemy battlefield units to receive -2 until end of turn
  - locks friendly battlefield and both base units as unaffected
  - records skipped optional movement under the current single-battlefield-area model
  - preserves the negative-power boundary by ending one enemy battlefield unit at `power: -1`

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysMoonRiseEnemyBattlefieldPowerMinus2`
  - `P4DeclareBattleNegativePowerAttackerDealsNoCombatDamageAndRetainsTruePower`
  - `P7PostStackCleanupDoesNotDestroyUndamagedNegativePowerFieldUnit`
  - `P7PostStackCleanupDestroysZeroOrNegativePowerUnitWithDamage`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysMoonRiseEnemyBattlefieldPowerMinus2|FullyQualifiedName~P4DeclareBattleNegativePowerAttackerDealsNoCombatDamageAndRetainsTruePower|FullyQualifiedName~P7PostStackCleanupDoesNotDestroyUndamagedNegativePowerFieldUnit|FullyQualifiedName~P7PostStackCleanupDestroysZeroOrNegativePowerUnitWithDamage"
```

结果：5/5 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoonRise|FullyQualifiedName~PowerModifier|FullyQualifiedName~PowerMinus|FullyQualifiedName~NegativePower|FullyQualifiedName~Cleanup|FullyQualifiedName~CombatDamage|FullyQualifiedName~Stack|FullyQualifiedName~Priority"
```

结果：196/196 passed。

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
  - `stage4CBatch78MoonRiseEnemyBattlefieldPowerMinus2Evidence`
  - `functionalUnits[].stage4C78`
  - `snapshotEntries[].stage4C78`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Moon Rise representative enemy battlefield -2 power-modifier route and negative-power boundary evidence，不清零 multi-battlefield selection、optional enemy movement、complete cleanup replacement / duration-effect matrix、complete FEPR、hidden-info 或 full-official 缺口。
