# Stage 4C-92 Stern Sergeant Experience Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `UNL-157/219` registered as direct card behavior
  - cost `6`
  - `EffectKind: STERN_SERGEANT_PLAY_UNIT_GAIN_EXPERIENCE_STATIC`
  - `TargetCount: 0`
  - `PlaysSourceToBaseAsUnit: true`
  - `SourceUnitPower: 6`
  - `SourceUnitTags: 精锐`
  - `GainExperienceOnPlayPerFriendlyFieldUnit: 1`

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-stern-sergeant-experience-static.fixture.json`
  - records ordinary hand play / pay 6 / zero target stack route
  - locks `UNIT_PLAYED_TO_BASE`
  - locks `EXPERIENCE_GAINED`
  - locks final P1 base containing the source object
  - locks source unit 6 power, `CARD_TYPE:UNIT|精锐`, active state, empty stack, and spent mana pool
  - locks P1 experience changing from 0 to 1 when only Stern Sergeant itself is counted as a friendly field unit
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-stern-sergeant-dynamic-experience.fixture.json`
  - records the dynamic friendly field unit count branch
  - locks existing friendly base unit + friendly battlefield unit + source unit as 3 counted friendly units
  - locks friendly equipment and enemy unit not counting toward the experience amount
  - locks `EXPERIENCE_GAINED.amount=3` and P1 total experience 3
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysKeywordOnlySourceUnit`
  - `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided`
  - `P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits`
  - adjacent source-unit, experience, play-card, target, stack, priority, and payment tests

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits|FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided"
```

结果：460/460 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SternSergeant|FullyQualifiedName~DynamicExperience|FullyQualifiedName~Experience|FullyQualifiedName~KeywordOnlySourceUnit|FullyQualifiedName~SourceUnit|FullyQualifiedName~UnitToBase|FullyQualifiedName~PlayCard|FullyQualifiedName~Target|FullyQualifiedName~Stack|FullyQualifiedName~Priority|FullyQualifiedName~Payment|FullyQualifiedName~PayCost"
```

结果：1913/1913 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3771/3771 passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch92SternSergeantExperienceEvidence`
  - `functionalUnits[].stage4C92`
  - `snapshotEntries[].stage4C92`

4B freeze status 从 `IMPLEMENTED_UNTESTED` 升级为 `IMPLEMENTED_TESTED`，自动化状态改为 `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`；`fullOfficial=false` 保持不变。本批只入账 Stern Sergeant / 严厉军士 representative source-unit + dynamic friendly field unit experience route 与 target rejection，不声明完整 experience economy、full-official 或 final READY。
