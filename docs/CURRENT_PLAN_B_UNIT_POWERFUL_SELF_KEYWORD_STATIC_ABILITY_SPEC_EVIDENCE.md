# Plan B Unit Powerful Self Keyword Static Ability Spec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records evidence for routing the existing OGN Fiora powerful self-keyword representative through `BehaviorSpec.StaticAbilities` instead of a `CoreRuleEngine` card-number branch.

## 1. Official Rule Evidence

- Official catalog entry `OGN·232/298`: “如果我变为{{强力}}单位，则我获得{{法盾}}、{{游走}}和{{坚守}}。（战力达到5或以上时，即为强力单位。）”
- Local rule audit summary `docs/符文战场_服务端核心规则自查文档.md` records the same powerful threshold: current power 5 or higher.

These entries are sourced from `data/official/card-catalog.zh-CN.json` and the existing rules-audit documentation; no official data file was edited.

## 2. Runtime Evidence

- `RuleTextParser` parses the OGN Fiora static text into `StaticAbilitySpec` with:
  - `Kind=UNIT_POWERFUL_SELF_KEYWORDS` via `StaticAbilityKinds.UnitPowerfulSelfKeywords`
  - `RequiredPowerThreshold=5`
  - `GrantedKeywords=[法盾, 游走, 坚守]`
- `StaticAbilitySpec` now carries optional `RequiredPowerThreshold` and `GrantedKeywords`, and DevUi catalog typing mirrors those fields for shared catalog payload compatibility.
- `CardStaticAbilitySpecRules.TryGetUnitPowerfulSelfKeywordsAbility(...)` builds on the existing BehaviorSpec-backed static ability map and validates a positive threshold plus at least one granted keyword.
- `CoreRuleEngine.ApplyPowerThresholdSelfKeywordStaticAbilities(...)` queries `CardStaticAbilitySpecRules` after successful power-changing representative paths and applies the spec-defined keyword set once the current power reaches the threshold.
- `CoreRuleEngine` no longer defines `OgnFioraCardNo` or the old `ApplyOgnFioraPowerfulKeywordTags` helper.

## 3. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `BehaviorSpecCatalogParsesUnitPowerfulSelfKeywordStaticAbility` proves the official OGN Fiora entry produces the expected static ability row, threshold, granted keywords, text fragments and implemented catalog status.
- `UnitPowerfulSelfKeywordStaticDoesNotUseCoreCardNumberBranch` blocks reintroducing the old OGN Fiora card-number branch and requires the new `CardStaticAbilitySpecRules.TryGetUnitPowerfulSelfKeywordsAbility` call.
- Existing `CoreRuleEngineGrantsOgnFioraKeywordsWhenBoonMakesPowerful` proves the representative boon-to-powerful runtime behavior still grants `法盾`, `游走`, and `坚守`.

## 4. Verification

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

## 5. Non-Closure Statement

This evidence does not close complete powerful-unit official breadth, complete keyword grant / revoke layer semantics, full official OGN Fiora behavior, full PaymentEngine / PAY_COST breadth, card matrix full-official, frontend final validation, formal E2E or READY.
