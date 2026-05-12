# Stage 4C-86 Imperial Shrine Conquer Sand Soldier Evidence

审计日期：2026-05-13
结论：**代表性战场征服触发证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo = "SFD·207/221"`
  - `TryResolveBattlefieldConquerPayOneReturnUnitCreateSandSoldierTrigger`
  - `BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER`
- `src/Riftbound.Engine/MatchSession.cs`
  - `battlefield-conquer-sand-soldier` development seed
  - `BuildBattlefieldConquerSandSoldierScenario`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
  - `SFD·207/221` 已登记在 battlefield non-play rule domain。

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `P79BattlefieldConquerSandSoldierPaysOneReturnsUnitAndCreatesToken`
  - `P79BattlefieldConquerSandSoldierSkipsWhenManaUnavailable`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - `P79BattlefieldConquerSandSoldierSeedReturnsUnitAndCreatesToken`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerSandSoldier"
```

结果：3/3 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldConquer"
```

结果：45/45 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch86ImperialShrineConquerPayReturnSandSoldierGuard`
  - `functionalUnits[].stage4C86` for `FU-ec31812b00`
  - `snapshotEntries[].stage4C86` for `SFD·207/221`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Imperial Shrine / 帝王神坛 representative conquer pay-one return-unit create-Sand-Soldier route；complete optional trigger choice、PaymentEngine、battlefield lifecycle、FAQ p22、hidden-info / redaction、formal READY audit 与 full-official 仍 deferred。
