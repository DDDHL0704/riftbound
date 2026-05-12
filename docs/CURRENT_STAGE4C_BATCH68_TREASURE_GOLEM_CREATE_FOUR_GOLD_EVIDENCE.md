# Stage 4C-68 Treasure Golem Create Four Gold Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `SFD·174/221` / `TREASURE_GOLEM_PLAY_UNIT_CREATE_FOUR_GOLD`
  - `CreatedBaseEquipmentTokenCount: 4`
  - `CreatedBaseEquipmentTokenName: 金币`
  - `CreatedBaseEquipmentTokenTags: CARD_TYPE:EQUIPMENT`
  - `CreatedBaseEquipmentTokenIsExhausted: true`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 9`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - created equipment token 结算路径在 stack resolution 后把 token 创建到控制者基地。
  - 当前 Treasure Golem 路线为 0 目标；unexpected target 在支付、入栈、源牌入场和 Gold token 创建前被拒绝。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-treasure-golem-create-four-gold.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 8 / zero-target stack / pass-pass resolution
  - source enters base as 9-power unit
  - four exhausted `CARD_TYPE:EQUIPMENT` Gold tokens enter base
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-treasure-golem-target-rejected.fixture.json`
  - unexpected target rejected
  - no tick, events, payment, hand movement, stack item, source unit creation, or Gold token creation
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysTreasureGolemCreateFourGold`
  - `CoreRuleEngineRejectsTreasureGolemWhenTargetsAreProvided`
  - `P4TreasureGolemTargetRejectedFixture`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysTreasureGolemCreateFourGold|FullyQualifiedName~CoreRuleEngineRejectsTreasureGolemWhenTargetsAreProvided|FullyQualifiedName~P4TreasureGolemTargetRejectedFixture"
```

结果：3/3 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TreasureGolem|FullyQualifiedName~Gold|FullyQualifiedName~JungleAmbush|FullyQualifiedName~BloodMoney|FullyQualifiedName~PainfulPayoff|FullyQualifiedName~HonestBroker"
```

结果：30/30 passed。

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
  - `stage4CBatch68TreasureGolemCreateFourGoldEvidence`
  - `functionalUnits[].stage4C68`
  - `snapshotEntries[].stage4C68`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
