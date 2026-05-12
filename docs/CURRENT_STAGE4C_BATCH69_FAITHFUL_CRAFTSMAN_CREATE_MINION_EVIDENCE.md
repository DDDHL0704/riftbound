# Stage 4C-69 Faithful Craftsman Create Minion Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·211/298` / `FAITHFUL_CRAFTSMAN_PLAY_UNIT_CREATE_MINION`
  - `CreatedBaseUnitTokenCount: 1`
  - `CreatedBaseUnitTokenPower: 1`
  - `CreatedBaseUnitTokenName: 随从`
  - `CreatedBaseUnitTokenTags: CARD_TYPE:UNIT`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 2`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - created unit token 结算路径在 stack resolution 后把 token 创建到控制者基地。
  - `ApplyMinionTokenFamilyTag` 对 `tokenName == 随从` 追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
  - 当前 Faithful Craftsman 路线为 0 目标；unexpected target 在支付、入栈、源牌入场和 Minion token 创建前被拒绝。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-faithful-craftsman-create-minion.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 3 / zero-target stack / pass-pass resolution
  - source enters base as 2-power unit
  - one ready 1-power `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION` token enters base
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysFaithfulCraftsmanCreateMinion`
  - `CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysFaithfulCraftsmanCreateMinion|FullyQualifiedName~CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided"
```

结果：2/2 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~FaithfulCraftsman|FullyQualifiedName~MinionTokenFamily|FullyQualifiedName~CommonCause|FullyQualifiedName~FutureForge|FullyQualifiedName~VanguardCaptain|FullyQualifiedName~MechanicalTrickster|FullyQualifiedName~Viktor"
```

结果：18/18 passed。

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
  - `stage4CBatch69FaithfulCraftsmanCreateMinionEvidence`
  - `functionalUnits[].stage4C69`
  - `snapshotEntries[].stage4C69`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
