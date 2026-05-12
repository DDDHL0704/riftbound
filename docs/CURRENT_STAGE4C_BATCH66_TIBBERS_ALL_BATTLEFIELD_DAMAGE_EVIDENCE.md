# Stage 4C-66 Tibbers All Battlefield Damage Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGS·018/024` / `TIBBERS_PLAY_UNIT_DAMAGE_ALL_BATTLEFIELD_UNITS_3`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 7`
  - `DamagesAllBattlefieldUnits: true`
  - `DamageAmount: 3`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `DamagesAllBattlefieldUnits` 结算路径在 stack resolution 后枚举公开战场单位并造成指定伤害。
  - 该路径跳过非公开或非单位战场对象，不把对手控制的非单位战场对象错误卷入 damage-all-battlefield-unit 结算。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-tibbers-damage-all-battlefield-units.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 8 / pass-pass resolution
  - source enters base as 7-power unit
  - public battlefield units each take 3 damage
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysTibbersDamageAllBattlefieldUnits`
  - `CoreRuleEngineTibbersResolutionSkipsOpponentControlledBattlefieldObject`
  - `CoreRuleEngineRejectsTibbersWhenTargetsAreProvided`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysTibbersDamageAllBattlefieldUnits|FullyQualifiedName~CoreRuleEngineTibbersResolutionSkipsOpponentControlledBattlefieldObject|FullyQualifiedName~CoreRuleEngineRejectsTibbersWhenTargetsAreProvided"
```

结果：3/3 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Tibbers|FullyQualifiedName~BladeWhirlwind|FullyQualifiedName~DamageAllBattlefield|FullyQualifiedName~OverchargedEnergy|FullyQualifiedName~FirestormEnemyBattlefieldDamageGuardTests|FullyQualifiedName~CrescentStrike|FullyQualifiedName~EnemyBattlefield"
```

结果：63/63 passed。

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
  - `stage4CBatch66TibbersAllBattlefieldDamageEvidence`
  - `functionalUnits[].stage4C66`
  - `snapshotEntries[].stage4C66`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
