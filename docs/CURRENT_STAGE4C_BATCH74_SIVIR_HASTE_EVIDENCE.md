# Stage 4C-74 Sivir Haste Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·143/221` / `SFD·143a/221` registered as direct card behavior
  - cost `4`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 4`
  - `SourceUnitTags: 急速`
  - `HasteReadyManaCost: 1`
  - `HasteReadyPowerCost: 1`
  - `HasteReadyPowerTrait: RuneTrait.Purple`
- `tests/Riftbound.ConformanceTests/Fixtures`
  - `p2-preflight-play-sivir-no-optional-haste.fixture.json`
  - `p2-preflight-play-sivir-alt-a-no-optional-haste.fixture.json`
  - `p4-play-sivir-haste-ready.fixture.json`
  - `p4-play-sivir-alt-a-haste-ready.fixture.json`

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost`
  - `CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided`
  - `P4HasteOptionalReadyBranchPaysManaAndPowerForSivir`
  - `P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA`
  - `P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForSivir|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA|FullyQualifiedName~P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower"
```

结果：78/78 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Sivir|FullyQualifiedName~Haste|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune|FullyQualifiedName~PayCost|FullyQualifiedName~PowerByTrait"
```

结果：103/103 passed。

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
  - `stage4CBatch74SivirHasteEvidence`
  - `functionalUnits[].stage4C74`
  - `snapshotEntries[].stage4C74`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Sivir shared-oracle Haste representative routes，不清零完整 PaymentEngine、LayerEngine、cleanup/control、wild-rune / +2 / Roam 或 full-official 缺口。
