# Stage 4C-63 AnyUnit Target Scope Guard Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `CardTargetScopes.AnyUnit` 收紧为 public field-unit scope。
  - `IsFieldUnitObjectControlledByZonePlayer` 排除 face-down / standby / equipment / spell / rune / dirty controller objects，同时兼容 trait-only unit objects。

## 测试证据

- `tests/Riftbound.ConformanceTests/AnyUnitTargetScopeGuardTests.cs`
  - `FirstMateReadiesOnlyPublicFieldUnitTargets`
  - `FirstMateRejectsNonPublicFieldUnitTargetsWithoutMutation`
  - `AnyUnitScopeRejectsNonUnitWhenBehaviorDoesNotRequireUnitTag`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~CoreRuleEnginePlaysFirstMateReadyAnotherUnit|FullyQualifiedName~CoreRuleEnginePlaysTheCurtainRises|FullyQualifiedName~CoreRuleEnginePlaysMirrorImageCreatesEphemeralCopyInBase"
```

结果：15/15 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~FirstMate|FullyQualifiedName~CurtainRises|FullyQualifiedName~Beatdown|FullyQualifiedName~AnyUnit|FullyQualifiedName~TargetScope|FullyQualifiedName~TargetGuard"
```

结果：16/16 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3742/3742 passed。

```sh
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

结果：frontend build passed；Chrome smoke passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch63AnyUnitTargetScopeGuard`
  - `functionalUnits[].stage4C63`
  - `snapshotEntries[].stage4C63`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
