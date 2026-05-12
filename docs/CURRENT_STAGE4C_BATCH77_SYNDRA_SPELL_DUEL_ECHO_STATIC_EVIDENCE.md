# Stage 4C-77 Syndra Spell Duel Echo Static Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-146/219` registered as direct card behavior
  - cost `6`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 6`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-syndra-spell-duel-echo-static.fixture.json`
  - records ordinary hand play / pay 6 / zero-target stack / source-to-base unit route
  - locks final `power: 6`
  - locks final tags as `CARD_TYPE:UNIT`
  - explicitly defers spell-duel detection, Echo grant, and repeated spell effects

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysVanillaSourceUnit`
  - `CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided|FullyQualifiedName~SpellDuel|FullyQualifiedName~Echo"
```

结果：361/361 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuel|FullyQualifiedName~Echo|FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~PowerByTrait|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~VanillaSourceUnit"
```

结果：553/553 passed。

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
  - `stage4CBatch77SyndraSpellDuelEchoStaticEvidence`
  - `functionalUnits[].stage4C77`
  - `snapshotEntries[].stage4C77`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Syndra representative ordinary play-to-base unit route and shared target-rejection evidence，不清零 actual spell-duel Echo grant、PaymentEngine、LayerEngine、FEPR 或 full-official 缺口。
