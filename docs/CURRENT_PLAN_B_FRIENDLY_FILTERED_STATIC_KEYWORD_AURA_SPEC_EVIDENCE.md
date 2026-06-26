# Plan B Friendly-Filtered Static Keyword Aura Spec Evidence

更新时间：2026-06-26

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
- Official-deck replay now covers `SFD·181/221` Rumble legend granting `坚守` to an official `SFD·026/221` friendly mechanical unit in a legal Rumble deck route; real defender damage records `basePower=4`, `keyword=坚守`, `keywordBonus=1`, `combatPower=5`, and `damage=5`, then score-victory action-log replay reaches the same final state hash.
- Official-deck replay now also covers `SFD·071/221` Speeding Mech projecting both `法盾` and `游走` RULE_TEXT effects to an official `SFD·026/221` friendly mechanical unit in a legal Rumble deck route; the target has no printed Spellshield/Roam tags, then the focused battle / score-victory action-log replay reaches the same final state hash.
- Source-tag `预知` permanents with no explicit look/target model now receive a shared lifecycle default in `CardBehaviorRegistry`, using the existing top-1 optional main-deck recycle path instead of per-card registrations. `OGN·100/298` Gemstone Seer is the representative runtime fixture.
- Static-granted `预知` from `FRIENDLY_FILTERED_UNITS_KEYWORD` now feeds the same lifecycle default when a public source already grants `预知` to a later-played matching friendly unit. `SFD·065/221` Prescient Mech plus `SFD·075/221` Progress Glory is the representative prompt and Core stack-resolution fixture.
- Hidden-boundary guards now cover `FRIENDLY_FILTERED_UNITS_KEYWORD`: face-down public-field sources do not project RULE_TEXT effects or grant combat keyword bonuses, and face-down matching friendly targets are excluded from continuous-effect target projection.

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
- `FullGameEndToEndTests.OfficialDeckMidgameAppliesRumbleLegendFriendlyMechanicalSteadfastAndScoreVictoryActionLogReplaysToFinalStateHash`
  - Verifies a legal official Lillia / Rumble deck opening can feed a focused midgame route where P2's `SFD·181/221` legend projects `FRIENDLY_FILTERED_UNITS_KEYWORD` / `坚守` to a P2 `SFD·026/221` mechanical defender at P1's battlefield.
  - Verifies the server-authored `DECLARE_BATTLE` path records `keywordBonus=1` defender damage and that score-victory action-log replay reaches the same final state hash.
- `FullGameEndToEndTests.OfficialDeckMidgameProjectsSpeedingMechFriendlyMechanicalSpellshieldRoamAndScoreVictoryActionLogReplaysToFinalStateHash`
  - Verifies a legal official Rumble deck opening can feed a focused midgame route where `SFD·071/221` Speeding Mech in base projects both `法盾` and `游走` through `FRIENDLY_FILTERED_UNITS_KEYWORD` to a friendly official `SFD·026/221` mechanical unit at the same battlefield as an official defender.
  - Verifies the target is not carrying printed Spellshield/Roam tags, then follows the server-authored `DECLARE_BATTLE` path through score-victory action-log replay to the same final state hash.
- `ConformanceFixtureRunnerTests.GemstoneSeerPredictPromptExposesOnlyFriendlyTopMainDeckCard`
  - Verifies source-tag `预知` lifecycle defaults expose only the controller's top main-deck card as an optional prompt target, excluding the second friendly card and the opponent's hidden deck card.
- `ConformanceFixtureRunnerTests.CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard`
  - Adds `OGN·100/298` Gemstone Seer as a source-tag `预知` representative that recycles the selected friendly top main-deck card through the shared engine path.
- `ConformanceFixtureRunnerTests.PrescientMechStaticGrantedPredictPromptExposesOnlyFriendlyTopMainDeckCardForMechanicalUnit`
  - Verifies public-field `SFD·065/221` grants `预知` to a later-played friendly mechanical hand unit and exposes only the controller's top main-deck card as an optional prompt target.
- `ConformanceFixtureRunnerTests.CoreRuleEnginePlaysStaticGrantedPredictSourceUnitRecycleTopCard`
  - Adds `p2-preflight-play-progress-glory-static-granted-predict-recycle.fixture.json` as a static-granted `预知` representative that plays a non-printed-Predict mechanical unit, resolves it to base, and recycles the selected top main-deck card.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordGrantDoesNotProjectFromFaceDownSource`
  - Verifies a face-down Rumble hero source does not project `FRIENDLY_FILTERED_UNITS_KEYWORD` RULE_TEXT effects and does not grant Assault combat keyword bonus even when authoritative test state still carries card number and tags.
- `ConformanceFixtureRunnerTests.P79FriendlyFilteredStaticKeywordGrantDoesNotProjectToFaceDownTarget`
  - Verifies a face-down matching friendly mechanical unit is not emitted as a RULE_TEXT continuous-effect target.

## Validation Results

- Focused previous combat-keyword slice: 4/4 passed.
- Focused multiple non-combat keyword slice: 2/2 passed.
- Adjacent FriendlyFiltered / StaticAura / StaticKeyword / Roam / Spellshield: 313/313 passed.
- Focused source-tag Predict lifecycle slice: 8/8 passed.
- Focused static-granted Predict lifecycle slice: 2/2 passed.
- Adjacent Predict / Gemstone / Lifecycle: 112/112 passed.
- Focused hidden-boundary slice: 2/2 passed.
- Adjacent FriendlyFiltered / StaticKeyword / StaticAura / Roam / Spellshield / Hidden / FaceDown / MatchRecovery: 2238/2238 passed.
- Focused Rumble legend official-deck replay slice: 1/1 passed.
- FullGameEndToEnd replay class after Rumble legend official-deck replay: 40/40 passed.
- Adjacent RumbleLegendFriendlyMechanicalSteadfast / FriendlyFiltered / StaticKeyword / StaticAura / Steadfast / Rumble / FullGameEndToEnd / MatchRecovery: 2112/2112 passed.
- Focused Speeding Mech official-deck replay slice: 1/1 passed.
- Focused Speeding Mech + dynamic non-combat keyword slice: 2/2 passed.
- FullGameEndToEnd replay class after Speeding Mech official-deck replay: 41/41 passed.
- Adjacent SpeedingMech / FriendlyFiltered / StaticKeyword / StaticAura / Spellshield / Roam / FullGameEndToEnd / MatchRecovery: 2195/2195 passed.
- MatchRecovery: 1989/1989 passed.
- Backend full conformance: 8592/8592 passed.
- Backend full after hidden-boundary guard: 8617/8617 passed.
- Backend full after Rumble legend official-deck replay: 8734/8734 passed.
- Backend full after Speeding Mech official-deck replay: 8735/8735 passed.

## Residuals

Not closed by this slice:

- Full keyword removal / loss layering.
- Static-granted `预知` breadth outside the covered public-source / later-played matching mechanical unit path, including simultaneous self-grant questions and broader Predict trigger sequencing.
- Non-combat friendly-filtered keyword grants outside the covered `预知` representative and `法盾` / `游走` runtime representatives.
- Rumble conquer recycle and graveyard mechanical play / cost reduction.
- Lillia token-play temporary power trigger.
- Card matrix FU-level fullOfficial readiness.
