# Stage 4C-73 Plunder Alley Battlefield Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
  - `OGN·285/298` included in implemented battlefield rule cards
  - effect domain: `BATTLEFIELD_RULE_DOMAIN`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `BattlefieldDefendMoveFriendlyUnitToBaseCardNo = "OGN·285/298"`
  - defender target choice is optional but limited to one defending unit
  - valid trigger emits `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_MOVED_TO_BASE`
  - dirty battlefield source not controlled by defender is rejected before mutation
- `src/Riftbound.Engine/MatchSession.cs`
  - development seed `battlefield-defend-move-to-base`
  - seeded battlefield object `P2-BATTLEFIELD-PLUNDER-ALLEY` uses cardNo `OGN·285/298`

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `P79BattlefieldDefendMovesChosenSurvivingDefenderToBase`
  - `P79BattlefieldDefendMoveToBaseRejectsAttackerControlledBattlefield`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - `P79BattlefieldDefendMoveToBaseSeedOffersBattlefieldDestinationAndChoice`
  - verifies prompt destination / target choice, localized invalid target rejection, event resolution, and final snapshot zone placement

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldDefendMovesChosenSurvivingDefenderToBase|FullyQualifiedName~P79BattlefieldDefendMoveToBaseRejectsAttackerControlledBattlefield|FullyQualifiedName~P79BattlefieldDefendMoveToBaseSeedOffersBattlefieldDestinationAndChoice"
```

结果：3/3 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldDefend|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BattlefieldControl|FullyQualifiedName~Plunder|FullyQualifiedName~BattlefieldDestination"
```

结果：137/137 passed。

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
  - `stage4CBatch73PlunderAlleyBattlefieldEvidence`
  - `functionalUnits[].stage4C73`
  - `snapshotEntries[].stage4C73`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Plunder Alley battlefield-domain representative route，不清零完整战场规则域、FAQ-wide 或 full-official 缺口。
