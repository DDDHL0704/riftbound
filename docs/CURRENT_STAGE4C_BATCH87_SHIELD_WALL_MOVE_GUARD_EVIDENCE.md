# Stage 4C-87 Shield Wall Move Guard Evidence

审计日期：2026-05-13
结论：**代表性移动目标 guard 证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·043/221`
  - `SHIELD_WALL_MOVE_ANY_FRIENDLY_BATTLEFIELD_UNITS_TO_BASE`
  - `TargetScope: FriendlyBattlefieldUnit`
  - `MinTargetCount: 0`
  - `MovesTargetToBase: true`
  - `UsesFriendlyBattlefieldUnitCountAsMaxTargetCount: true`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysShieldWallAndMovesSelectedFriendlyBattlefieldUnitsToBase`
  - `CoreRuleEngineRejectsShieldWallAgainstEnemyBaseOrRepeatedTarget`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ShieldWall"
```

结果：2/2 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoveFriendly|FullyQualifiedName~MoveUnit|FullyQualifiedName~FriendlyBattlefieldUnit"
```

结果：63/63 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch87ShieldWallMoveFriendlyBattlefieldUnitsGuard`
  - `functionalUnits[].stage4C87` for `FU-a7fbef72ba`
  - `snapshotEntries[].stage4C87` for `SFD·043/221`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Shield Wall / 禁军之墙 representative move-any-friendly-battlefield-units-to-base route；multi-battlefield movement、standby / quick timing、complete FEPR、complete PaymentEngine、hidden-info / redaction、formal READY audit 与 full-official 仍 deferred。
