# Stage 4C-76 Long Sword Equipment Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·022/221` registered as direct card behavior
  - cost `2`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsEquipment: true`
  - `SourceEquipmentTags: 武装|灵便`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-long-sword-agile-equipment.fixture.json`
  - records ordinary hand play / pay 2 / zero-target stack / source-to-base equipment route
  - locks `CARD_TYPE:EQUIPMENT` / `武装` / `灵便` tags
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-long-sword-target-rejected.fixture.json`
  - locks explicit-target rejection before mutation for the zero-target play route
- `tests/Riftbound.ConformanceTests/Fixtures/p4-assemble-equipment-long-sword-attach.fixture.json`
  - records minimal `ASSEMBLE_RED` attachment route
- `tests/Riftbound.ConformanceTests/Fixtures/p5-equipment-state-assemble-long-sword-owner-controller.fixture.json`
  - records owner/controller identity through the representative attachment route

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysLongSwordAgileEquipment`
  - `CoreRuleEngineRejectsLongSwordWhenTargetsAreProvided`
  - `P4LongSwordTargetRejectedFixture`
  - `P4AssembleEquipmentCommandLongSwordAttachFixture`
  - `P5EquipmentStateAssembleLongSwordOwnerControllerFixture`
  - `P5MoveUnitAttachedEquipmentFollowsHostFixture`
  - `P5EquipmentDetachesWhenHostDestroyedFixture`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LongSword|FullyQualifiedName~P4AssembleEquipmentCommandLongSwordAttachFixture|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P5EquipmentDetachesWhenHostDestroyedFixture"
```

结果：11/11 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LongSword|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~Equipment|FullyQualifiedName~Attached|FullyQualifiedName~Attach|FullyQualifiedName~MoveUnit|FullyQualifiedName~NonUnitSource"
```

结果：336/336 passed。

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
  - `stage4CBatch76LongSwordEquipmentEvidence`
  - `functionalUnits[].stage4C76`
  - `snapshotEntries[].stage4C76`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Long Sword representative equipment play / target rejection / minimal assemble identity route，不清零 Agile reaction attach、full equipment lifecycle、LayerEngine、PaymentEngine、FEPR 或 full-official 缺口。
