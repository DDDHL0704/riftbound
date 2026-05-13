# Stage 4C-94 Babbling Poro Predict Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-224/219` registered as direct card behavior
  - cost `2`
  - `EffectKind: UNL_BABBLING_PORO_PLAY_UNIT_PREDICT`
  - `TargetScope: CardTargetScopes.FriendlyMainDeckCard`
  - `MinTargetCount: 0`
  - `MainDeckLookCount: 1`
  - `RecyclesSelectedMainDeckTargets: true`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 2`
  - `SourceUnitTags: 魄罗|预知`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-unl-babbling-poro-predict-recycle.fixture.json`
  - records ordinary hand play / pay 2 / friendly main-deck top-card target route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks `CARDS_RECYCLED`
  - locks final P1 base containing the source object
  - locks source unit 2 power, `CARD_TYPE:UNIT|预知|魄罗`, active state, empty stack, spent mana pool, and main deck order with the selected top card moved to bottom
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard`
  - `CoreRuleEngineRejectsPredictSourceUnitWhenTargetIsOutsideTopCard`
  - adjacent predict, source-unit, play-card, target, stack, priority, and payment tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard|FullyQualifiedName~CoreRuleEngineRejectsPredictSourceUnitWhenTargetIsOutsideTopCard"
```

结果：12/12 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Predict|FullyQualifiedName~FriendlyMainDeck|FullyQualifiedName~MainDeck|FullyQualifiedName~Recycle|FullyQualifiedName~SourceUnit|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~Poro"
```

结果：1830/1830 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch94BabblingPoroPredictEvidence`
  - `functionalUnits[].stage4C94`
  - `snapshotEntries[].stage4C94`

4B freeze status 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。本批只入账 UNL Babbling Poro / 叨叨魄罗 representative source-unit + predict recycle route 与 outside-top-card target rejection，不声明完整 predict optional/no-recycle branches、full-official 或 final READY。
