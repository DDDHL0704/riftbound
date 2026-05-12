# Stage 4C-64 Enemy Battlefield Unit Target Scope Guard Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `CardTargetScopes.EnemyBattlefieldUnit` 收紧为 enemy public battlefield-unit scope。
  - `IsEnemyBattlefieldUnitObject` 要求目标位于敌方 `BATTLEFIELD`，并通过 public battlefield-unit controller guard。

## 测试证据

- `tests/Riftbound.ConformanceTests/EnemyBattlefieldUnitTargetScopeGuardTests.cs`
  - `MegasharkCannonDamagesOnlyEnemyPublicBattlefieldUnitTarget`
  - `MegasharkCannonRejectsNonPublicEnemyBattlefieldUnitTargetsWithoutMutation`
  - `CrescentStrikeRejectsEnemyBattlefieldNonUnitWithoutTargetRequiredTag`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests|FullyQualifiedName~CoreRuleEnginePlaysMegasharkCannonDamageEnemyBattlefield|FullyQualifiedName~CoreRuleEngineRejectsMegasharkCannonWhenTargetIsFriendlyUnit|FullyQualifiedName~CoreRuleEnginePlaysCrescentStrikeTargetPlusSplash|FullyQualifiedName~CoreRuleEngineRejectsCrescentStrikeAgainstFriendlyOrBaseUnit|FullyQualifiedName~P4CrescentStrike"
```

结果：18/18 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests|FullyQualifiedName~Megashark|FullyQualifiedName~CrescentStrike|FullyQualifiedName~ZenithBlade|FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~EnemyBattlefieldUnit|FullyQualifiedName~HostileTakeoverGuardTests|FullyQualifiedName~IsolateMoveToBaseGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~RideTheWindMoveGuardTests"
```

结果：82/82 passed。

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
  - `stage4CBatch64EnemyBattlefieldUnitTargetScopeGuard`
  - `functionalUnits[].stage4C64`
  - `snapshotEntries[].stage4C64`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
