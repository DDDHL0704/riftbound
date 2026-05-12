# Stage 4C-65 Demacia Envoy Experience Static Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-092/219` / `DEMACIA_ENVOY_PLAY_UNIT_GAIN_EXPERIENCE_STATIC`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 2`
  - `GainExperienceOnPlay: 1`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `GainExperienceOnPlay` 结算路径在 stack resolution 后为控制者增加经验，并记录 `EXPERIENCE_GAINED`。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-demacia-envoy-experience-static.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 2 / pass-pass resolution
  - source enters base as 2-power unit
  - P1 experience becomes 1
  - `GAINED_EXPERIENCE_THIS_TURN:P1`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysP2PreflightFixture`
  - `P4FixedExperienceGainOnPlayUpdatesControllerExperience`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysP2PreflightFixture|FullyQualifiedName~P4FixedExperienceGainOnPlayUpdatesControllerExperience|FullyQualifiedName~resource-experience|FullyQualifiedName~ResourceExperience"
```

结果：4/4 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Demacia|FullyQualifiedName~Experience|FullyQualifiedName~GainExperience|FullyQualifiedName~LevelThreshold"
```

结果：37/37 passed。

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
  - `stage4CBatch65DemaciaEnvoyExperienceStaticEvidence`
  - `functionalUnits[].stage4C65`
  - `snapshotEntries[].stage4C65`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
