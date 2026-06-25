# Plan B Friendly-Filtered Static Keyword Aura Spec Evidence

更新时间：2026-06-25

## Evidence Summary

This slice turns friendly-filtered static keyword grants from card-specific behavior into a shared BehaviorSpec-driven engine path.

Implemented evidence:

- `RuleTextParsers.StaticAuraParser` parses `你的指示物单位获得{{...}}` and `你的“...”属性单位获得{{...}}` into `StaticAuraSpec.Kind=FRIENDLY_FILTERED_UNITS_KEYWORD`.
- The parser now preserves multiple granted keywords in one sentence, e.g. `你的“机械”属性单位获得{{法盾}}和{{游走}}`, as separate `StaticAuraSpec` entries instead of collapsing by shared text.
- `StaticAuraSpecRules.GetStaticAuras(cardNo, kind)` exposes all parsed specs of the same kind to shared engine layers.
- `MatchSession.BuildFriendlyFilteredUnitsKeywordAuraEffects` projects RULE_TEXT continuous effects from public-field unit sources and legend-zone sources to matching friendly public units.
- `CoreRuleEngine.ResolveFriendlyFilteredUnitsKeywordBonus` applies dynamic Assault / Steadfast / Roam combat keyword amounts during battle power and movement permission checks.
- Spellshield target-tax calculation reads dynamic friendly-filtered Spellshield grants for both action prompts and Core payment plans.
- Battle damage assignment legality and ordering now read dynamic friendly-filtered Bulwark / Back Row grants instead of only printed tags.
- The prior Rumble legend steadfast special case has been removed; `SFD·181/221` and `SFD·240/221` now flow through `FRIENDLY_FILTERED_UNITS_KEYWORD`.
- Source-tag `预知` permanents with no explicit look/target model now receive a shared lifecycle default in `CardBehaviorRegistry`, using the existing top-1 optional main-deck recycle path instead of per-card registrations. `OGN·100/298` Gemstone Seer is the representative runtime fixture.

## Covered Cards

| Card | Official text | BehaviorSpec |
|---|---|---|
| `SFD·026/221` / `SFD·026a/221` 兰博 | `你的“机械”属性单位获得{{强攻}}。` | `TargetFilter=TAG:机械`, `GrantedKeyword=强攻` |
| `SFD·065/221` 先见机甲 | `你的“机械”属性单位获得{{预知}}。` | `TargetFilter=TAG:机械`, `GrantedKeyword=预知` |
| `SFD·071/221` 疾驰机械 | `你的“机械”属性单位获得{{法盾}}和{{游走}}。` | two specs: `GrantedKeyword=法盾` and `GrantedKeyword=游走` |
| `SFD·181/221` / `SFD·240/221` 机械公敌 | `你的“机械”属性单位获得{{坚守}}。` | `TargetFilter=TAG:机械`, `GrantedKeyword=坚守` |
| `UNL-058/219` / `UNL-058a/219` 莉莉娅 | `你的指示物单位获得{{壁垒}}。` | `TargetFilter=UNIT_TOKEN`, `GrantedKeyword=壁垒` |

## Tests

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives`
  - Verifies the covered card faces parse to `FRIENDLY_FILTERED_UNITS_KEYWORD`, `RULE_TEXT`, `WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD`, `FRIENDLY_FILTERED_UNITS`, `FRIENDLY_FILTERED_PUBLIC_UNITS`, target filter, and granted keyword.
  - Verifies `SFD·071/221` produces two keyword aura specs for one official sentence.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordGrantsKeywordsToMatchingFriendlyUnits`
  - Verifies public-field Rumble hero source grants Assault only to friendly mechanical units.
  - Verifies Rumble legend source grants Steadfast only to friendly mechanical units.
  - Verifies Lillia grants Bulwark only to friendly unit tokens.
  - Verifies Assault contributes `keywordBonus=1` to mechanical attacker combat power.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordGrantsMultipleNonCombatKeywordsToMatchingFriendlyUnits`
  - Verifies `SFD·071/221` grants both Spellshield and Roam RULE_TEXT effects to matching friendly mechanical units while excluding non-mechanical and opposing units.
  - Verifies the action prompt exposes dynamic Roam movement for a mechanical unit with no printed Roam tag.
  - Verifies Core accepts the dynamic Roam precise battlefield movement.
  - Verifies Core charges one Spellshield target-tax mana when a spell targets an enemy mechanical unit that only has Spellshield from the static aura.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordBulwarkSupportsMultiDefenderAssignment`
  - Verifies Lillia's dynamic Bulwark grant makes a friendly unit token eligible as the assignment keyword defender in a multi-defender declaration.
- `ConformanceFixtureRunnerTests.P79LegendStaticRumbleGrantsSteadfastToMechanicalDefender`
  - Verifies Rumble legend Steadfast is projected as a RULE_TEXT continuous effect and contributes `keywordBonus=1` to a mechanical defender.
- `ConformanceFixtureRunnerTests.GemstoneSeerPredictPromptExposesOnlyFriendlyTopMainDeckCard`
  - Verifies source-tag `预知` lifecycle defaults expose only the controller's top main-deck card as an optional prompt target, excluding the second friendly card and the opponent's hidden deck card.
- `ConformanceFixtureRunnerTests.CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard`
  - Adds `OGN·100/298` Gemstone Seer as a source-tag `预知` representative that recycles the selected friendly top main-deck card through the shared engine path.

## Validation Results

- Focused previous combat-keyword slice: 4/4 passed.
- Focused multiple non-combat keyword slice: 2/2 passed.
- Adjacent FriendlyFiltered / StaticAura / StaticKeyword / Roam / Spellshield: 313/313 passed.
- Focused source-tag Predict lifecycle slice: 8/8 passed.
- Adjacent Predict / Gemstone / Lifecycle: 110/110 passed.
- MatchRecovery: 1989/1989 passed.
- Backend full conformance: 8590/8590 passed.

## Residuals

Not closed by this slice:

- Full keyword removal / loss layering.
- Prediction execution for friendly-filtered static-granted `预知` grants, including top-deck look/recycle prompts.
- Non-combat friendly-filtered keyword grants outside the covered `预知` parse/projection and `法盾` / `游走` runtime representatives.
- Rumble conquer recycle and graveyard mechanical play / cost reduction.
- Lillia token-play temporary power trigger.
- Card matrix FU-level fullOfficial readiness.
