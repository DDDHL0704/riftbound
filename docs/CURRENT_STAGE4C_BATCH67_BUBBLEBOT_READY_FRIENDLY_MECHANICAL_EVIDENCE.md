# Stage 4C-67 Bubblebot Ready Friendly Mechanical Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·062/221` / `BUBBLEBOT_PLAY_UNIT_READY_FRIENDLY_MECHANICAL`
  - `TargetScope: FriendlyUnit`
  - `TargetRequiredTag: CARD_TYPE:UNIT|机械`
  - `ReadiesTarget: true`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 3`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ReadiesTarget` 结算路径在 stack resolution 后让目标对象变为活跃。
  - `FriendlyUnit` + target tag guard 要求目标为友方公开单位并带 `机械` 标签；非机械目标在支付、入栈和移动手牌前被拒绝。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bubblebot-ready-friendly-mechanical.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 3 / friendly Mechanical target / pass-pass resolution
  - source enters base as 3-power unit
  - target friendly Mechanical unit becomes ready
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysBubblebotReadyFriendlyMechanical`
  - `CoreRuleEngineRejectsBubblebotWhenTargetIsNotMechanical`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBubblebotReadyFriendlyMechanical|FullyQualifiedName~CoreRuleEngineRejectsBubblebotWhenTargetIsNotMechanical"
```

结果：2/2 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Bubblebot|FullyQualifiedName~Mechanical|FullyQualifiedName~FirstMate|FullyQualifiedName~HuntReady|FullyQualifiedName~AnyUnitTargetScopeGuardTests"
```

结果：32/32 passed。

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
  - `stage4CBatch67BubblebotReadyFriendlyMechanicalEvidence`
  - `functionalUnits[].stage4C67`
  - `snapshotEntries[].stage4C67`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
