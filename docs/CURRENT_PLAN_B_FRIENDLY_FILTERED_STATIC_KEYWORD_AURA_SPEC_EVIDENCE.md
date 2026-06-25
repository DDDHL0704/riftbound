# Plan B Friendly-Filtered Static Keyword Aura Spec Evidence

更新时间：2026-06-25

## Evidence Summary

This slice turns friendly-filtered static keyword grants from card-specific behavior into a shared BehaviorSpec-driven engine path.

Implemented evidence:

- `RuleTextParsers.StaticAuraParser` parses `你的指示物单位获得{{...}}` and `你的“...”属性单位获得{{...}}` into `StaticAuraSpec.Kind=FRIENDLY_FILTERED_UNITS_KEYWORD`.
- `StaticAuraSpecRules.TryGetFriendlyFilteredUnitsKeywordAura` exposes the parsed spec to shared engine layers.
- `MatchSession.BuildFriendlyFilteredUnitsKeywordAuraEffects` projects RULE_TEXT continuous effects from public-field unit sources and legend-zone sources to matching friendly public units.
- `CoreRuleEngine.ResolveFriendlyFilteredUnitsKeywordBonus` applies dynamic Assault / Steadfast combat keyword amounts during battle power calculation.
- Battle damage assignment legality and ordering now read dynamic friendly-filtered Bulwark / Back Row grants instead of only printed tags.
- The prior Rumble legend steadfast special case has been removed; `SFD·181/221` and `SFD·240/221` now flow through `FRIENDLY_FILTERED_UNITS_KEYWORD`.

## Covered Cards

| Card | Official text | BehaviorSpec |
|---|---|---|
| `SFD·026/221` / `SFD·026a/221` 兰博 | `你的“机械”属性单位获得{{强攻}}。` | `TargetFilter=TAG:机械`, `GrantedKeyword=强攻` |
| `SFD·181/221` / `SFD·240/221` 机械公敌 | `你的“机械”属性单位获得{{坚守}}。` | `TargetFilter=TAG:机械`, `GrantedKeyword=坚守` |
| `UNL-058/219` / `UNL-058a/219` 莉莉娅 | `你的指示物单位获得{{壁垒}}。` | `TargetFilter=UNIT_TOKEN`, `GrantedKeyword=壁垒` |

## Tests

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives`
  - Verifies the six covered card faces parse to `FRIENDLY_FILTERED_UNITS_KEYWORD`, `RULE_TEXT`, `WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD`, `FRIENDLY_FILTERED_UNITS`, `FRIENDLY_FILTERED_PUBLIC_UNITS`, target filter, and granted keyword.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordGrantsKeywordsToMatchingFriendlyUnits`
  - Verifies public-field Rumble hero source grants Assault only to friendly mechanical units.
  - Verifies Rumble legend source grants Steadfast only to friendly mechanical units.
  - Verifies Lillia grants Bulwark only to friendly unit tokens.
  - Verifies Assault contributes `keywordBonus=1` to mechanical attacker combat power.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordBulwarkSupportsMultiDefenderAssignment`
  - Verifies Lillia's dynamic Bulwark grant makes a friendly unit token eligible as the assignment keyword defender in a multi-defender declaration.
- `ConformanceFixtureRunnerTests.P79LegendStaticRumbleGrantsSteadfastToMechanicalDefender`
  - Verifies Rumble legend Steadfast is projected as a RULE_TEXT continuous effect and contributes `keywordBonus=1` to a mechanical defender.

## Validation Results

- Focused: 4/4 passed.
- Adjacent FriendlyFiltered / StaticAura / StaticKeyword / BattleDamageAssignment / MatchRecovery: 2085/2085 passed.
- MatchRecovery: 1989/1989 passed.
- Backend full conformance: 8587/8587 passed.

## Residuals

Not closed by this slice:

- Full keyword removal / loss layering.
- Non-combat friendly-filtered keyword grants outside the covered combat keyword representatives.
- Rumble conquer recycle and graveyard mechanical play / cost reduction.
- Lillia token-play temporary power trigger.
- Card matrix FU-level fullOfficial readiness.
