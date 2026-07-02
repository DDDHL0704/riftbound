# Plan B Unit Powerful Self Keyword Static Ability Spec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `OGN·232/298` 菲奥娜“变为强力单位后获得法盾、游走、坚守”的既有代表性结算，从 `CoreRuleEngine` 的单卡卡号分支迁移到官方文本解析出的 `BehaviorSpec.StaticAbilities`。该切片只收窄已有 OGN Fiora representative 的 source / threshold / granted-keyword 来源，不关闭完整强力判定、完整 RULE_TEXT layer、关键词撤销/重复实例、Fiora full official、P0 full objective 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.DevUi/src/types/catalog.ts`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_POWERFUL_SELF_KEYWORD_STATIC_ABILITY_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_POWERFUL_SELF_KEYWORD_STATIC_ABILITY_SPEC_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- `ApplyBoon` and until-end-of-turn power modifier event payload names
- existing OGN Fiora keyword grant timing in the representative path
- generic continuous-effect RULE_TEXT projection semantics
- keyword payment / movement / defense keyword runtimes
- card matrix full-official status

## 2. Official Inputs

- `data/official/card-catalog.zh-CN.json`: `OGN·232/298` contains “如果我变为{{强力}}单位，则我获得{{法盾}}、{{游走}}和{{坚守}}。（战力达到5或以上时，即为强力单位。）”
- `docs/符文战场_服务端核心规则自查文档.md`: strong/powerful unit summary states units are powerful when current power is 5 or higher.

## 3. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| OGN Fiora static text is represented as data | `RuleTextParser` emits `StaticAbilityKinds.UnitPowerfulSelfKeywords` with `RequiredPowerThreshold=5` and `GrantedKeywords=[法盾, 游走, 坚守]` | Accepted |
| Runtime reads static ability shape from shared spec rules | `CoreRuleEngine` now calls `CardStaticAbilitySpecRules.TryGetStaticAbility(..., IsUnitPowerfulSelfKeywordsAbility, ...)` after boon / temporary power changes | Accepted |
| Core no longer owns the OGN Fiora card-number branch | `OgnFioraCardNo` and `ApplyOgnFioraPowerfulKeywordTags` were removed from `CoreRuleEngine`; the guard test blocks reintroduction | Accepted |
| Existing representative behavior remains intact | Existing `CoreRuleEngineGrantsOgnFioraKeywordsWhenBoonMakesPowerful` still passes through the same boon-to-powerful path | Accepted |
| Frontend shared catalog type stays aligned | `src/Riftbound.DevUi/src/types/catalog.ts` now mirrors optional static ability `amount`, `requiredPowerThreshold`, and `grantedKeywords` payload fields | Accepted |
| Full official breadth | complete layer dependency, dynamic removal, simultaneous timing, keyword instance semantics, and full Fiora official behavior remain residual | Residual, no full-official claim |

## 4. Verification

Initial focused TDD guard failed before implementation because the shared contract did not yet expose `StaticAbilityKinds.UnitPowerfulSelfKeywords`. After implementation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitPowerfulSelfKeywordStaticAbility|FullyQualifiedName~UnitPowerfulSelfKeywordStaticDoesNotUseCoreCardNumberBranch|FullyQualifiedName~CoreRuleEngineGrantsOgnFioraKeywordsWhenBoonMakesPowerful" --nologo
```

Result: 3/3 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OgnFiora|FullyQualifiedName~Fiora|FullyQualifiedName~UnitPowerfulSelfKeyword|FullyQualifiedName~UnitCannotBecomeActive|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~Boon|FullyQualifiedName~ApplyBoon|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2390/2390 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8815/8815 passed.

```sh
/opt/homebrew/bin/npm --prefix src/Riftbound.DevUi run build
```

Result: passed. npm emitted existing config warnings, and Vite emitted existing Rollup annotation / chunk-size warnings.

## 5. Residual Risks

- This does not implement a general “became powerful” trigger family beyond the existing representative power-change paths.
- This does not rework granted keywords into a full continuous RULE_TEXT layer with dependency/timestamp recalculation.
- This does not prove keyword removal if later power changes below threshold; the current representative keeps the existing gained-keyword behavior.
- This does not close full official OGN Fiora behavior, complete powerful-unit breadth, full keyword timing/payment breadth, card matrix full-official, frontend final validation, formal E2E or READY.
