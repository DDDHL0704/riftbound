# Plan B B1 Static Aura Spec Audit

更新时间：2026-06-30

## Scope

These B1 slices advance Plan B by moving implemented `STATIC_AURA` projection and combat-power recompute surfaces from card-number allow-lists toward `BehaviorSpec` data.

## 2026-06-30 Supplement: Battlefield RULE_TEXT Keyword Aura Scope Router

This follow-up merges the battlefield all-units RULE_TEXT keyword aura and battlefield filtered-units RULE_TEXT keyword aura combat/projection selectors into shared `StaticAuraSpec` scope routing.

`CoreRuleEngine` now computes battlefield keyword bonuses through `ResolveBattlefieldKeywordStaticAuraBonus`, enumerating `StaticAuraSpecRules.GetStaticAuras(battlefieldState.CardNo)` and filtering with `StaticAuraSpecRules.IsBattlefieldKeywordStaticAura`. The actual participant applicability is derived from the same all-units / filtered-units target and participant scope pair used by the POWER router, while filtered scopes still use `StaticAuraSpecRules.TargetMatchesFilter`.

`MatchSession` now projects battlefield keyword continuous effects through `BuildBattlefieldKeywordAuraEffects`, enumerating each source battlefield's `BehaviorSpec.StaticAuras` instead of calling kind-specific `TryGetBattlefieldAllUnitsKeywordAura` / `TryGetBattlefieldFilteredUnitsKeywordAura` helpers. Existing `RULE_TEXT:BATTLEFIELD_ALL_UNITS_KEYWORD` and `RULE_TEXT:BATTLEFIELD_FILTERED_UNITS_KEYWORD` effect id shapes are preserved.

`StaticAuraSpecRules.HasBattlefieldKeywordStaticAura` now backs battlefield-card recognition for battlefield keyword aura sources. Roam prompt / movement permission still uses the existing `TryGetBattlefieldAllUnitsGrantedKeywordAura(cardNo, MoveUnitRoamKeyword, out _)` query, but that query now also reads the shared battlefield keyword aura predicate.

Validation: red/green focused `BattlefieldStaticAuraSpecRoutingGuardTests.BattlefieldKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope` 1/1; battlefield keyword / Roam focused regression 63/63; StaticAura / StaticKeyword / Battlefield / Roam / MoveUnit / FullGameEndToEnd / MatchRecovery adjacent 2329/2329; backend full conformance 9060/9060.

Non-closure: this slice only consolidates battlefield all-units/filtered keyword aura combat and projection routing. Other RULE_TEXT aura families, movement timing breadth, complete B2 keyword breadth, full LayerEngine timestamp/order semantics, full B1/B2, P0, and READY remain open.

## 2026-06-30 Supplement: Battlefield POWER Static Aura Scope Router

This follow-up merges the battlefield all-units POWER aura and battlefield filtered-units POWER aura runtime/projection selectors into shared `StaticAuraSpec` scope routing.

`CoreRuleEngine` now computes battlefield POWER static-aura bonuses through `ResolveBattlefieldPowerStaticAuraBonus`, enumerating `StaticAuraSpecRules.GetStaticAuras(battlefieldState.CardNo)` and filtering with `StaticAuraSpecRules.IsBattlefieldPowerStaticAura`. The actual participant applicability is derived from `StaticAuraTargetScopes.SameBattlefieldUnits` / `SameBattlefieldFilteredUnits` and the matching participant scope, with filtered scopes still using `StaticAuraSpecRules.TargetMatchesFilter`.

`MatchSession` now projects those same battlefield POWER aura families through `BuildBattlefieldPowerStaticAuraEffects`, again enumerating the source battlefield card's `BehaviorSpec.StaticAuras` instead of calling kind-specific `TryGetBattlefieldAllUnitsPowerAura` / `TryGetBattlefieldFilteredUnitsPowerAura` helpers. Existing `ContinuousEffectState` effect ids, source paths, conditions and lifecycle strings are preserved for recovery compatibility.

`StaticAuraSpecRules.HasBattlefieldPowerStaticAura` now backs battlefield-card recognition in both Core and MatchSession, so adding a new supported battlefield POWER aura with the same target/participant scope shape no longer requires a second all-units-specific rule-card recognition branch.

Validation: red/green focused `BattlefieldStaticAuraSpecRoutingGuardTests` 1/1; battlefield/static-aura focused behavior regression 34/34; StaticAura / StaticPower / Battlefield / FullGameEndToEnd / MatchRecovery adjacent 2843/2843; backend full conformance 9059/9059.

Non-closure: this slice only consolidates the battlefield POWER static-aura all-units/filtered scope router. Battlefield RULE_TEXT aura routing, object-source aura families, friendly-equipment source power, full static-aura timestamp/order semantics, full B1, P0, and READY remain open.

## 2026-06-30 Supplement: Shared Battlefield All-Units Keyword Aura Query

This follow-up removes the remaining duplicated `BattlefieldSourceGrantsRoam` private selector from `CoreRuleEngine` and `MatchSession`. Both runtime and prompt paths now call `StaticAuraSpecRules.TryGetBattlefieldAllUnitsGrantedKeywordAura(cardNo, MoveUnitRoamKeyword, out _)` for Wind Hill-style battlefield all-units `RULE_TEXT` keyword permission.

The change is source-selector only: `OGN·297/298` 疾风山丘 still parses as `StaticAuraSpec.Kind=BATTLEFIELD_ALL_UNITS_KEYWORD` with `GrantedKeyword=游走`, battlefield keyword sources must remain face-up, and existing movement / Roam command semantics are unchanged.

Validation: focused guard red/green `StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList` 1/1; StaticAura / BattlefieldStaticRoam / BattlefieldAllUnits / Roam / MoveUnit / CardCatalogBaseline / MatchRecovery adjacent 2490/2490; backend full conformance 9049/9049.

Implemented in this slice:

- `BehaviorSpec.StaticAuras` protocol/catalog surface.
- Parser coverage for the currently implemented static-power representatives:
  - `SFD·085/221` / `SFD·085a/221` 奥恩：每有一件友方装备，自身 `{{S}}+1`。
  - `OGN·294/298` 崔法利兵营：此处所有单位 `{{S}}+1`。
  - `OGS·013/024` 盖伦、`SFD·236/221` / `SFD·236*/221` 德莱厄斯、`OGN·243/298` / `OGN·243a/298` 德莱厄斯：此处其他友方单位 `{{S}}+1`。
  - `UNL-147/219` / `UNL-147a/219` / `UNL-238/219` 纳什男爵：其他友方单位 `{{S}}+2`。
  - `UNL-077/219` 牧魂人：你的指示物单位获得 `{{S}}+1`。
  - `SFD·089/221` / `SFD·089a/221` 兰博：你的“机械”属性单位获得 `{{S}}+1`。
  - `OGN·151/298` / `OGN·151a/298` 李青：我所在战场上其他拥有增益的友方单位获得 `{{S}}+2`。
  - `UNL·T03` 草丛：此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得 `{{S}}+1`。
  - `UNL-076/219` 花瓣仙子：我所处的战场你每有一名拥有 `{{瞬息}}` 的单位，我便获得 `{{S}}+1`。
  - `OGN·240/298` / `OGN·240a/298` 瑟提：我所处的战场每有一名拥有增益的友方单位，我便获得 `{{S}}+1`。
  - `SFD·159/221` 可靠攻城犬：如果你在此处有其他单位，则自身获得 `{{S}}+1`。
  - `OGS·019/024` 无极剑圣：如果你只有一名友方单位防守一处战场，则该单位 `{{S}}+2`。
  - `UNL-191/219` / `UNL-231/219` / `UNL-231*/219` 无极宗师：`{{等级6>}}` 你的单位获得 `{{S}}+1`。
  - `UNL-016/219` 焰爪、`UNL-047/219` 踏苔蜥、`UNL-075/219` 风行狐、`UNL-094/219` 晶手猎人、`UNL-098/219` 巨神峰先知：`{{等级N>}}` 自身获得 `{{S}}+X`。
  - `UNL-154/219` 猩红飞鸽：如果和另一名单位一起进攻一处战场，则自身 `{{S}}+2`。
  - `OGN·055/298` 驭水者：如果独自进攻或防守一处战场，则自身 `{{S}}+2`。
  - `OGN·131/298` 沙丘亚龙：进攻时如果此处有处于活跃状态的敌方单位，则自身 `{{S}}+2`。
  - `OGN·065/298` 睿智长者：如果自身拥有增益，则自身 `{{S}}+1`。
  - `OGN·297/298` 疾风山丘：此处的单位获得 `{{游走}}`。
  - `UNL-210/219` 禁忌荒地：如果防守此处的单位落单，则该单位 `{{S}}-2`。
  - `UNL-041/219` 艾蕾，头号拥趸：如果自身位于战场上，同战场其他友方单位获得 `{{法盾}}`。
- Parser false-positive guards:
  - `UNL-043/219` 热情的播报员：其 card text grants `{{增益}}` tokens and must not be treated as a fixed `STATIC_AURA` power modifier.
  - `UNL-195/219` 翠神：parenthetical reminder text describes the Brush battlefield token and must not be treated as a legend-source `STATIC_AURA`.
- `MatchSession` continuous-effect projection now resolves these `STATIC_AURA` kinds via `StaticAuraSpecRules` instead of `ContinuousEffectStaticAuraCards`.
- `CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus`, `CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus`, `CoreRuleEngine.ResolveBattlefieldAllUnitsKeywordBonus`, `CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus`, `CoreRuleEngine.ResolveSourceSameLocationOtherFriendlyUnitPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus`, `CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus`, and `CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus` now apply these static power / keyword auras from `BehaviorSpec.StaticAuras`.
- `StaticAuraSpec.RequiredParticipantCount` models threshold-style source auras where one or more same-location participants enable a fixed power delta rather than multiplying by every participant.
- `StaticAuraSpec.RequiredPlayerExperience` models level/experience-gated static auras without hard-coding the source card in the engine.
- `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` and source-unit entry power now resolve Ornn-style friendly-equipment count-to-source static power through `StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura` and `StaticAuraSpec.PowerDeltaPerParticipant`; the former `CardBehaviorDefinition.AddsFriendlyFieldEquipmentCountToSourceUnitPower` runtime bridge has been deleted.
- Friendly-equipment static-power hidden-boundary guard: standby friendly-equipment count-to-source power sources no longer project `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER` `STATIC_AURA` effects or recompute source power from public equipment count.
- `CoreRuleEngine` resolves Master Yi intro single-defender power and Master Yi level friendly-unit power through `StaticAuraSpec.Kind=FRIENDLY_SINGLE_DEFENDING_UNIT_POWER` and `StaticAuraSpec.Kind=FRIENDLY_UNITS_POWER`; the old `HasMasterYiSingleDefenderBonus` / `ResolveMasterYiLevelLegendPowerBonus` combat-power special paths have been deleted.
- `CoreRuleEngine.ResolveSourceObjectPowerBonus` and `MatchSession.TryBuildSourceObjectPowerStaticAuraEffect` now resolve level-gated source-object power (`SOURCE_OBJECT_POWER`) from `BehaviorSpec.StaticAuras`, using `StaticAuraSpec.RequiredPlayerExperience` and `PowerDeltaPerParticipant` rather than a source card-number branch.
- `StaticAuraSpecRules.IsSourceObjectPowerAuraAlreadyMaterialized` prevents legacy source-unit entry paths that already stored the same level power in the authoritative object power from double-counting the new spec-driven combat resolver; this compatibility guard is data-driven by the card behavior's level power metadata and the parsed aura delta.
- `MatchSession` now projects source-object combat static auras for `SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `SOURCE_LONE_BATTLE_POWER`, and `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` from `BehaviorSpec.StaticAuras` while the current battle state satisfies their parsed participant-count conditions.
- Source-combat static-aura hidden-boundary guard: standby battle participants are excluded from battle attacker/defender participant evaluation and from source-combat participant / dependency metadata, so standby objects do not enable source-combat static auras or appear in their participant lists.
- Source-combat Core recompute hidden-boundary guard: standby source units are excluded from the `SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `SOURCE_LONE_BATTLE_POWER`, and `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` combat-power helpers, matching the projection layer.
- `tests/Riftbound.ConformanceTests/StaticAuraStackingAndModifierTests.cs` now covers additive stacking between a source-combat `STATIC_AURA`, a non-local other-friendly `STATIC_AURA`, and an until-end-of-turn `POWER_MODIFIER` in both continuous-effect projection and real combat damage.
- The old generic `ResolveWaterbenderLoneBattlePowerBonus` method name has been replaced by `ResolveSourceLoneBattlePowerBonus` so the source path no longer names a single representative card.
- `MatchRecovery` now validates source-object combat static-aura `powerDelta` as the fixed BehaviorSpec power delta rather than multiplying by the participant object count.
- `CardEquipmentKeywordRules` marks the Ornn friendly-equipment static-power representative boundary from `BehaviorSpec.StaticAuras` instead of a registry runtime flag.
- `CoreRuleEngine.HasBattlefieldStaticRoamPermission` and `ActionPromptBuilder.HasMoveUnitPromptRoamPermission` now grant `ROAM` from `StaticAuraSpec.Kind=BATTLEFIELD_ALL_UNITS_KEYWORD` + `GrantedKeyword=游走` instead of the old `BattlefieldStaticRoamCardNo` branch.
- `CoreRuleEngine.ResolveSpellshieldTargetTaxMana` and `ActionPromptBuilder.FriendlyFilteredUnitsGrantedSpellshieldTax` now apply same-battlefield other-friendly `RULE_TEXT` keyword auras with `GrantedKeyword=法盾` from `BehaviorSpec.StaticAuras`.
- Battlefield keyword aura hidden-boundary guard: battlefield keyword sources that are face-down no longer project `BATTLEFIELD_*_UNITS_KEYWORD` RULE_TEXT effects, grant combat keyword bonuses, or provide static Roam prompt / movement permission.
- `MatchSession` now projects battlefield isolated-defender RULE_TEXT keyword modifiers from `BehaviorSpec.StaticAuras` when the active battle has exactly one public defender at the source battlefield; the projection path has no card-number branch.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `SFD·181/221` Rumble legend's friendly-mechanical Steadfast RULE_TEXT aura in a legal official Rumble deck midgame route: after legal official deck submission/opening is verified, a focused `START_BATTLE` state stages an official `SFD·026/221` Rumble mechanical defender and `OGN·096/298` Watchful Sentinel at the same P1 battlefield, the legend-source `FRIENDLY_FILTERED_UNITS_KEYWORD` route grants the defender `坚守`, defender damage records `keywordBonus=1`, and the action log replays through score victory to the same final state hash.
- Battlefield static-power hidden-boundary guard: battlefield power sources that are face-down no longer project `BATTLEFIELD_*_UNITS_POWER` `STATIC_AURA` effects or grant combat static-power bonuses; all-units battlefield power targets that are face-down / standby are excluded from participant projection and Core static-power bonus.
- Other-friendly static-power hidden-boundary guard: standby non-local other-friendly power sources no longer project `OTHER_FRIENDLY_UNITS_POWER` `STATIC_AURA` effects or grant combat static-power bonuses; standby target units are excluded from other-friendly participant projection.
- Same-battlefield other-friendly static-power hidden-boundary guard: standby same-battlefield other-friendly power sources no longer project `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` `STATIC_AURA` effects or grant combat static-power bonuses; standby same-battlefield target units are excluded from participant projection and Core static-power bonus.
- `MatchSession` projects the Master Yi level legend aura as a legend-source `FRIENDLY_UNITS_POWER` continuous effect when the controller satisfies the required experience threshold; static-aura source ordering/dependencies now include public legend-zone sources.
- Recovery static-aura source-card validation now checks the source card's `BehaviorSpec` aura surface for battlefield all-units, battlefield-filtered, same-battlefield friendly-filtered count-to-source, same-battlefield other-friendly, same-battlefield friendly-filtered, non-local other-friendly, friendly-units, and friendly-filtered unit auras instead of a projection allow-list.
- `tests/Riftbound.ConformanceTests/WiseElderSourceObjectFilteredPowerTests.cs` now covers `SOURCE_OBJECT_FILTERED_POWER` participating in real `DECLARE_BATTLE` damage: Wise Elder with `CardObjectTags.Boon` deals 5 damage from base 4 plus `staticPowerBonus=1`.
- `tests/Riftbound.ConformanceTests/SourceObjectLevelPowerStaticAuraTests.cs` now covers `SOURCE_OBJECT_POWER` participating in continuous-effect projection and real `DECLARE_BATTLE` damage: Crystalhand Hunter at 6 experience projects `STATIC_AURA:SOURCE_OBJECT_POWER:*` with `PowerDelta=1`, stays absent below 6 experience, and deals 3 damage from base 2 plus `staticPowerBonus=1`.
- `tests/Riftbound.ConformanceTests/SameBattlefieldOtherFriendlyStaticPowerCardRowTests.cs` now covers the official same-battlefield other-friendly static-power card rows (`OGS·013/024`, `SFD·236/221`, `SFD·236*/221`, `OGN·243/298`, `OGN·243a/298`) in continuous-effect projection and real `DECLARE_BATTLE` damage.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGS·013/024` Garen's same-battlefield other-friendly static aura in a legal official Poppy deck route: server prompts play and move Garen plus `UNL-092/219` Demacia Envoy to the same battlefield, the spec-driven continuous effect targets the Envoy, real battle damage records `staticPowerBonus=1`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now also covers `SFD·236/221` Darius's same-battlefield other-friendly static aura in a legal official Darius deck route: server prompts play and move Darius plus `SFD·006/221` Aggressive Dragonhound to the same battlefield, the spec-driven continuous effect targets the Dragonhound, real battle damage records `basePower=3`, `staticPowerBonus=1`, `combatPower=4`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `SFD·085/221` Ornn's friendly-equipment count-to-source static aura in a legal official Rumble deck route: an official `SFD·022/221` Long Sword is staged as a public friendly equipment, server prompts play and move Ornn to a battlefield, the spec-driven `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER` continuous effect targets Ornn itself with the Long Sword as participant, real battle damage records recomputed `basePower=5` without an extra `staticPowerBonus`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-041/219` Aerie Head Fan's same-battlefield other-friendly `法盾` RULE_TEXT aura in a legal official Jhin vs Lillia deck route: server prompts play and move Aerie Head Fan plus official `OGN·096/298` Watchful Sentinel to the same battlefield, the spec-driven continuous effect grants `法盾` to the Sentinel, official `OGS·003/024` Incinerate targeting that unit pays `baseManaCost=2`, `spellshieldTaxMana=1`, and `totalManaCost=3`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-147/219` Baron Nashor's non-local other-friendly static aura in a legal official Vex deck route: server prompts play Baron Nashor to base and move `UNL-057/219` Wildclaw Beastmaster to a battlefield, the spec-driven continuous effect targets Wildclaw from the non-local source, real battle damage records `staticPowerBonus=2`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-154/219` Scarlet Pigeon's source-combat static aura in a legal official Poppy deck route: server prompts play and move Scarlet Pigeon plus `UNL-092/219` Demacia Envoy, the server-authored `DECLARE_BATTLE` includes both as attackers, Scarlet Pigeon's source-combat route records `staticPowerBonus=2`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·131/298` Dune Drake's source-attacking-ready-enemy static aura in a legal official Poppy deck route: server prompts play and move Dune Drake plus an opposing ready defender to the same battlefield, the server-authored `DECLARE_BATTLE` has Dune Drake attacking alone, Dune Drake's `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` route records `staticPowerBonus=2`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-076/219` Petal Pixie's same-battlefield ephemeral count-to-source static aura in a legal official Lillia deck midgame route: after legal official deck submission/opening is verified, a focused `START_BATTLE` state stages Petal Pixie, official `UNL·T07` Faerie token and opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield, Petal Pixie's `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` route records `staticPowerBonus=1`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-077/219` Soul Shepherd's friendly-token static aura in a legal official Lillia deck midgame route: after legal official deck submission/opening is verified, a focused `START_BATTLE` state stages Soul Shepherd in base, official `UNL·T02` Warhawk token and opposing `UNL-057/219` Wildclaw Beastmaster at the same P1 battlefield, Soul Shepherd's `FRIENDLY_FILTERED_UNITS_POWER` route records token `staticPowerBonus=1`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `SFD·089/221` Rumble's friendly-mechanical static aura in a legal official Rumble deck midgame route: after legal official deck submission/opening is verified, a focused `START_BATTLE` state stages Rumble and opposing `OGN·096/298` Watchful Sentinel at the same P1 battlefield, Rumble's `FRIENDLY_FILTERED_UNITS_POWER` route uses the official `机械` tag filter, boosts Rumble itself, records `staticPowerBonus=1`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/BattlefieldIsolatedDefenderKeywordModifierProjectionTests.cs` and `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now cover `UNL-210/219` Forbidden Wasteland's battlefield isolated-defender RULE_TEXT keyword modifier: projection tests prove single-defender projection and multi-defender absence, while the official Vex / Rumble route records LeBlanc defender damage with `keyword=坚守`, `keywordBonus=-2`, `combatPower=2`, and score-victory replay to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·055/298` Waterbender's source-lone-battle static aura in a legal official Lillia deck route: server prompts play and move Waterbender plus opposing `OGN·096/298` Watchful Sentinel to the same battlefield, the server-authored `DECLARE_BATTLE` has Waterbender attacking alone, Waterbender's `SOURCE_LONE_BATTLE_POWER` route records `staticPowerBonus=2`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGS·019/024` Master Yi intro's friendly single-defender static aura in a legal official Master Yi intro deck route: server prompts play and move `UNL-092/219` Demacia Envoy to the opposing battlefield, the server-authored `DECLARE_BATTLE` has Demacia Envoy as the only defender, Master Yi intro's `FRIENDLY_SINGLE_DEFENDING_UNIT_POWER` route records `staticPowerBonus=2`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-191/219` Master Yi level's experience-gated friendly-units static aura in a legal official Master Yi level deck route: the route starts at 5 experience, uses `UNL-092/219` Demacia Envoy's server-resolved on-play text to reach level 6, projects `FRIENDLY_UNITS_POWER` from the legend source to the Envoy, records attacker `staticPowerBonus=1`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·065/298` Wise Elder's source-object filtered static aura in a legal official Master Yi level green/orange deck route: server prompts stage Wise Elder to a battlefield, `OGN·136/298` Arena Rookie grants Wise Elder `{{增益}}` through targeted `PLAY_CARD`, the spec-driven continuous effect targets Wise Elder itself with `SOURCE_OBJECT_FILTERED_POWER`, Wise Elder records boon-adjusted `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-094/219` Crystalhand Hunter's source-object level static aura in a legal official Poppy deck route: the route starts at 6 experience, server prompts stage Crystalhand Hunter and opposing `OGN·096/298` Watchful Sentinel to the same battlefield, the spec-driven continuous effect targets Crystalhand Hunter itself with `SOURCE_OBJECT_POWER`, Crystalhand Hunter records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-098/219` Targon Seer's source-object level static aura in a legal official Poppy deck route: the route starts at 11 experience, server prompts stage Targon Seer and opposing `OGN·096/298` Watchful Sentinel to the same battlefield, the spec-driven continuous effect targets Targon Seer itself with `SOURCE_OBJECT_POWER`, `PowerDelta=4`, and `RequiredPlayerExperience=11`, Targon Seer records `basePower=6`, `staticPowerBonus=4`, `combatPower=10`, and `damage=10`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `UNL-016/219` Flameclaw's source-object level static aura together with its level-gated source-unit active-entry static ability in a legal official Jhin deck route: the route starts at 3 experience, server prompts play Flameclaw directly to a battlefield, `SOURCE_UNIT_ENTER_READY` makes the source unit enter ready with self source metadata, the spec-driven continuous effect targets Flameclaw itself with `SOURCE_OBJECT_POWER`, `PowerDelta=1`, and `RequiredPlayerExperience=3`, Flameclaw records `basePower=3`, `staticPowerBonus=1`, `combatPower=4`, and `damage=4`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·294/298` Trifarian Training Grounds' battlefield all-units static aura in a legal official Vex deck route: the driver probes official-opening seeds until P1 selects Trifarian Training Grounds, server prompts move `UNL-057/219` Wildclaw Beastmaster and an opposing defender to that battlefield, the spec-driven continuous effect targets both units from the battlefield source, Wildclaw records `staticPowerBonus=1`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·297/298` Wind Hill / 疾风山丘's battlefield all-units RULE_TEXT keyword aura in a legal official Vex deck route: the driver probes official-opening seeds until P1 selects Wind Hill, server prompts move `UNL-057/219` Wildclaw Beastmaster to Wind Hill, the spec-driven continuous effect grants `游走` from the battlefield source without adding a printed tag, the server-authored precise battlefield `MOVE_UNIT` pays `ROAM` to move that unit to a second official P1 battlefield object, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `SFD·159/221` Reliable Siege Dog's source same-location threshold static aura in a legal official Poppy deck route: server prompts play and move Reliable Siege Dog plus `UNL-092/219` Demacia Envoy to the same battlefield, the spec-driven continuous effect targets Reliable Siege Dog itself with the Envoy as participant, Reliable Siege Dog records `staticPowerBonus=1`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·240/298` Sett's same-battlefield boon count-to-source static aura in a legal official Poppy deck route: server prompts stage `UNL-092/219` Demacia Envoy to a battlefield, `OGN·136/298` Arena Rookie grants the Envoy `{{增益}}` through targeted `PLAY_CARD`, Sett moves to the same battlefield, the spec-driven continuous effect targets Sett itself with the boon-bearing Envoy as participant, Sett records `staticPowerBonus=1`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now covers `OGN·151/298` Lee Sin's same-battlefield other-friendly filtered static aura in a legal official Poppy deck route: server prompts stage `UNL-092/219` Demacia Envoy to a battlefield, `OGN·136/298` Arena Rookie grants the Envoy `{{增益}}` through targeted `PLAY_CARD`, Lee Sin moves to the same battlefield, the spec-driven continuous effect targets the boon-bearing Envoy but not Lee Sin, the Envoy records `staticPowerBonus=2`, and the full action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/BrushStaticAuraReplacementLifecycleTests.cs` now covers Brush's battlefield-filtered `STATIC_AURA` remaining active through a score-time replacement choice: matching attacker / defender units receive `staticPowerBonus=1`, and the same `DECLARE_BATTLE` uses `BRUSH_USE_REPLACED_BATTLEFIELD:*` to resolve the replaced Energy Hub held-score trigger.

## Not Closed

This slice does not claim full B1 completion:

- Battlefield card recognition still has an implemented-battlefield-card registry; this slice only removes card-number gating from the static-power bonus arithmetic.
- Current non-local other-friendly aura coverage is the fixed static-power family only; Nash battlefield-token creation, replacement entry destination, and enemy spell/skill target protection remain open.
- Brush score-time replacement now has a combined static-aura representative, but broader battlefield-token replacement / actual swap-back lifecycle is still not closed by this B1 slice.
- Garen and Darius same-battlefield other-friendly, Ornn friendly-equipment count-to-source, Baron Nashor non-local other-friendly, Scarlet Pigeon source-combat, Dune Drake source-attacking-ready-enemy, Petal Pixie same-battlefield ephemeral count-to-source, Soul Shepherd friendly-token filtered, Rumble friendly-mechanical filtered self-boost, Rumble legend friendly-mechanical Steadfast, Forbidden Wasteland battlefield isolated-defender keyword modifier, Waterbender source-lone-battle, Master Yi intro friendly single-defender, Master Yi level friendly-units, Wise Elder source-object filtered, Flameclaw / Crystalhand Hunter / Targon Seer source-object level power, Trifarian Training Grounds battlefield all-units, Reliable Siege Dog source same-location threshold, Sett same-battlefield boon count-to-source, and Lee Sin same-battlefield other-friendly filtered now have representative evidence, but broader source-object level family breadth and other static-aura families still need comparable official-deck routes before full B1/B2 breadth can be claimed.
- Broader multiple-aura stacking beyond the current source-combat + other-friendly + until-end power representative, full LayerEngine timestamp ordering, additional conditional subscopes beyond the same-location threshold representative, additional RULE_TEXT keyword grants / modifiers, and full official static-aura breadth remain open.
- Master Yi `{{等级11>}}` active-entry text is now tracked by `docs/CURRENT_PLAN_B_OTHER_FRIENDLY_ACTIVE_ENTRY_STATIC_ABILITY_SPEC_AUDIT.md`; it remains outside this B1 combat/static-power slice.
- Current `private|public static bool Is*CardNo(...)` helper count remains 0.
- Project remains NOT READY.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·085/221` / `SFD·085a/221`: `每有一件友方装备，我便获得{{S}}+1。`
- `OGN·294/298`: `此处的所有单位获得{{S}}+1。（包括进攻方单位。）`
- `OGS·013/024`: `此处的其他友方单位获得{{S}}+1。`
- `SFD·236/221` / `SFD·236*/221` / `OGN·243/298` / `OGN·243a/298`: `此处的其他友方单位获得{{S}}+1。`
- `UNL-147/219` / `UNL-147a/219` / `UNL-238/219`: `其他友方单位获得{{S}}+2。`
- `UNL-077/219`: `你的指示物单位获得{{S}}+1。`
- `SFD·089/221` / `SFD·089a/221`: `你的“机械”属性单位获得{{S}}+1。（包括我。）`
- `OGN·151/298` / `OGN·151a/298`: `我所在战场上其他拥有增益的友方单位获得{{S}}+2。`
- `UNL·T03`: `此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得{{S}}+1。`
- `UNL-076/219`: `我所处的战场你每有一名拥有{{瞬息}}的单位，我便获得{{S}}+1。`
- `OGN·240/298` / `OGN·240a/298`: `我所处的战场每有一名拥有增益的友方单位，我便获得{{S}}+1。`
- `SFD·159/221`: `如果你在此处有其他单位，则我获得{{S}}+1。`
- `OGS·019/024`: `如果你只有一名友方单位防守一处战场，则该单位{{S}}+2。`
- `UNL-191/219` / `UNL-231/219` / `UNL-231*/219`: `{{等级6>}} 你的单位获得{{S}}+1。`
- `UNL-154/219`: `如果我和另一名单位一起进攻一处战场，则我获得{{S}}+2。`
- `OGN·055/298`: `如果我独自进攻或防守一处战场，则我获得 {{S}}+2。`
- `OGN·131/298`: `当我进攻时，如果此处有处于活跃状态的敌方单位，则让我{{S}}+2。`
- `OGN·065/298`: `如果我拥有增益，则我额外获得{{S}}+1。`
- `OGN·297/298`: `此处的单位获得{{游走}}。（他们可以向其他战场进行移动。）`
- `UNL-210/219`: `如果防守此处的单位落单，则该单位{{S}}-2。`
- `UNL-041/219`: `如果我位于战场上，则你此处的其他单位获得{{法盾}}。`
- `UNL-043/219`: `给予此处的所有单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- `UNL-195/219`: `位于草丛的“鸟类”、“猫科”、“犬形”、“魄罗”和“艾翁”属性单位获得{{S}}+1。` appears only as parenthetical token reminder text.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefieldStaticSpellshieldAura|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives" --no-restore --nologo
```

Result: 5/5 passed.

Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT adjacent / hidden-info check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefieldStaticSpellshieldAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~StaticAura|FullyQualifiedName~MatchRecovery" --no-restore --nologo
```

Result: 2100/2100 passed.

Latest backend full after Aerie Head Fan same-battlefield Spellshield RULE_TEXT evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8929/8929 passed.

Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT official-deck replay focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesAerieHeadFanSameBattlefieldSpellshieldTax" --no-restore --nologo
```

Result: 1/1 passed.

Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT official-deck replay adjacent / hidden-info check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~AerieHeadFan|FullyQualifiedName~SameBattlefieldStaticSpellshieldAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery" --no-restore --nologo
```

Result: 2141/2141 passed.

Latest backend full after Aerie Head Fan same-battlefield Spellshield RULE_TEXT official-deck replay evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8941/8941 passed.

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79OtherFriendlyStaticPowerAddsTwoAcrossPublicField"
```

Result: 2/2 passed for the original B1 representative slice.

Latest same-battlefield friendly-filtered focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~SameBattlefieldOtherFriendlyFilteredStaticPower"
```

Result: 2/2 passed.

Latest same-battlefield friendly-filtered count-to-source focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~PetalPixieCountsFriendlyEphemeral|FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList"
```

Result: 3/3 passed.

Latest Sett same-battlefield boon count-to-source focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79SettCountsFriendlyBoonUnitsAtSameBattlefieldForBattlePower"
```

Result: 79/79 passed.

Latest battlefield-filtered focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~BattlefieldFilteredStaticPower"
```

Result: 2/2 passed.

2026-06-26 battlefield-filtered keyword hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldFilteredStaticKeywordGrantDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 battlefield keyword hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldFiltered|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~Roam|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2206/2206 passed.

2026-06-26 backend full after battlefield keyword hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8619/8619 passed.

2026-06-26 battlefield-filtered static-power hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldFilteredStaticPowerDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 battlefield static-power hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldFiltered|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2192/2192 passed.

2026-06-26 backend full after battlefield static-power hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8621/8621 passed.

2026-06-26 battlefield all-units static-power hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldAllUnitsStaticPowerDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 battlefield all-units static-power hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~BattlefieldAllUnits|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2193/2193 passed.

2026-06-26 backend full after battlefield all-units static-power hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8623/8623 passed.

2026-06-26 other-friendly static-power standby hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79OtherFriendlyStaticPowerDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 other-friendly static-power standby hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OtherFriendly|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Hidden|FullyQualifiedName~Standby|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2229/2229 passed.

2026-06-26 backend full after other-friendly static-power standby hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8625/8625 passed.

2026-06-26 same-battlefield other-friendly static-power standby hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldOtherFriendlyStaticPowerDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 same-battlefield other-friendly static-power standby hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Hidden|FullyQualifiedName~Standby|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2233/2233 passed.

2026-06-26 backend full after same-battlefield other-friendly static-power standby hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8627/8627 passed.

2026-06-26 friendly-equipment static-power standby hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerDoesNotProjectFromStandbySource" --nologo
```

Result: 1/1 passed.

2026-06-26 friendly-equipment static-power standby hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ornn|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Standby|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2257/2257 passed.

2026-06-26 backend full after friendly-equipment static-power standby hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8628/8628 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~LayerEngineBattlefieldStaticAura|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79OtherFriendlyStaticPower|FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffect|FullyQualifiedName~FullGameEndToEndTests"
```

Result: 356/356 passed.

Latest adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect"
```

Result: 399/399 passed.

Latest adjacent after battlefield-filtered slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect"
```

Result: 400/400 passed.

Latest adjacent after same-battlefield count-to-source slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PetalPixie"
```

Result: 401/401 passed.

Latest adjacent after Sett same-battlefield boon count-to-source slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PetalPixie|FullyQualifiedName~Sett"
```

Result: 427/427 passed.

Latest battlefield all-units keyword focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList|FullyQualifiedName~P79BattlefieldStaticRoam|FullyQualifiedName~P79BattlefieldStaticRoamSeedAllowsPreciseBattlefieldMove"
```

Result: 6/6 passed.

Latest battlefield all-units keyword adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~BattlefieldStaticRoam|FullyQualifiedName~MoveUnit|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~FullGameEndToEndTests"
```

Result: 711/711 passed.

Latest MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Latest Ornn friendly-equipment runtime bridge removal focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~EquipmentKeyword"
```

Result: 42/42 passed.

Latest same-location source threshold focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ReliableSiegeDogSameLocationStaticPowerTests|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives"
```

Result: 5/5 passed.

Latest Master Yi legend static-aura spec focused checks:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaselineTests"
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79LegendStaticMasterYi"
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MasterYiLegendStaticAuraSpecTests"
```

Result: 252/252, 3/3, and 2/2 passed.

Latest Master Yi legend static-aura adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~MasterYi|FullyQualifiedName~MatchRecovery"
```

Result: 2033/2033 passed.

Latest source combat static-aura projection and recovery scalar focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection"
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraPowerDeltaConsistencyDrift"
```

Result: 3/3 and 4/4 passed.

Latest source combat static-aura projection adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~P79ScarletPigeon|FullyQualifiedName~P79Waterbender|FullyQualifiedName~P79DuneDrake|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~MatchRecovery"
```

Result: 2041/2041 passed.

2026-06-26 source-combat standby participant hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ScarletPigeonSourceCombatStaticAuraIgnoresStandbyAttackerParticipants|FullyQualifiedName~DuneDrakeSourceCombatStaticAuraIgnoresStandbyReadyDefenderParticipants" --nologo
```

Result: 2/2 passed.

2026-06-26 source-combat static-aura projection focused after standby participant guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection" --nologo
```

Result: 5/5 passed.

2026-06-26 source-combat static-aura / recovery adjacent after standby participant guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~P79ScarletPigeon|FullyQualifiedName~P79Waterbender|FullyQualifiedName~P79DuneDrake|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2044/2044 passed.

2026-06-26 backend full after source-combat standby participant guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8631/8631 passed.

2026-06-26 source-combat standby source Core recompute focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79ScarletPigeonSkipsSourceCombatPowerWhenSourceIsStandby|FullyQualifiedName~P79DuneDrakeSkipsSourceCombatPowerWhenSourceIsStandby|FullyQualifiedName~P79WaterbenderSkipsSourceCombatPowerWhenSourceIsStandby" --nologo
```

Result: 3/3 passed.

2026-06-26 source-combat static-aura / recovery adjacent after standby source Core guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~P79ScarletPigeon|FullyQualifiedName~P79Waterbender|FullyQualifiedName~P79DuneDrake|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2047/2047 passed.

2026-06-26 backend full after source-combat standby source Core guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8634/8634 passed.

Latest static-aura stacking / until-end power modifier focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAuraStackingAndModifier" --nologo
```

Result: 1/1 passed.

Latest static-aura stacking / source-combat / recovery adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAuraStackingAndModifier|FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~P79ScarletPigeon|FullyQualifiedName~P79Waterbender|FullyQualifiedName~P79DuneDrake|FullyQualifiedName~P79OtherFriendlyStaticPower|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2043/2043 passed.

2026-06-26 source-object filtered static-power battle-damage focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~WiseElderSourceObjectFilteredPowerTests" --nologo
```

Result: 3/3 passed.

2026-06-26 source-object filtered static-power adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~WiseElderSourceObjectFilteredPowerTests|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~ReliableSiegeDog|FullyQualifiedName~MasterYiLegendStaticAuraSpecTests" --nologo
```

Result: 425/425 passed.

2026-06-26 source-object filtered static-power MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

2026-06-26 backend full after source-object filtered static-power evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8705/8705 passed.

2026-06-26 same-battlefield other-friendly static-power card-row focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefieldOtherFriendlyStaticPowerCardRowTests" --nologo
```

Result: 5/5 passed.

2026-06-26 same-battlefield other-friendly static-power card-row adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefieldOtherFriendlyStaticPowerCardRowTests|FullyQualifiedName~P79SameBattlefieldOtherFriendlyStaticPower|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect" --nologo
```

Result: 680/680 passed.

2026-06-26 same-battlefield other-friendly static-power card-row MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

2026-06-26 backend full after same-battlefield other-friendly static-power card-row evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8710/8710 passed.

2026-06-26 Brush static-aura + replacement lifecycle focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BrushStaticAuraReplacementLifecycleTests" --nologo
```

Result: 1/1 passed.

2026-06-26 Brush static-aura + replacement lifecycle adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BrushStaticAuraReplacementLifecycleTests|FullyQualifiedName~BrushReplacement|FullyQualifiedName~BattlefieldFilteredStaticPower|FullyQualifiedName~BattlefieldHeldScore|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect" --nologo
```

Result: 415/415 passed.

2026-06-26 Brush static-aura + replacement lifecycle MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

2026-06-26 backend full after Brush static-aura + replacement lifecycle evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8711/8711 passed.

2026-06-26 other-friendly static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesOtherFriendlyStaticAura"
```

Result: 1/1 passed.

2026-06-26 other-friendly static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result: 25/25 passed.

2026-06-26 other-friendly static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OtherFriendly|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Baron|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result: 2114/2114 passed.

2026-06-26 backend full after other-friendly static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8717/8717 passed.

2026-06-26 source-combat static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSourceCombatStaticAura"
```

Result: 1/1 passed.

2026-06-26 source-combat static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result: 26/26 passed.

2026-06-26 source-combat static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~SourceCombat|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Scarlet|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result: 2106/2106 passed.

2026-06-26 backend full after source-combat static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8718/8718 passed.

2026-06-26 battlefield all-units static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesBattlefieldAllUnitsStaticAura"
```

Result: 1/1 passed.

2026-06-26 battlefield all-units static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~FullGameEndToEndTests"
```

Result: 27/27 passed.

2026-06-26 battlefield all-units static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~BattlefieldAllUnits|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery"
```

Result: 2141/2141 passed.

2026-06-26 backend full after battlefield all-units static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8719/8719 passed.

2026-06-28 battlefield all-units RULE_TEXT Roam official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameProjectsBattlefieldAllUnitsStaticKeywordRoamAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result: 1/1 passed.

2026-06-28 battlefield all-units RULE_TEXT Roam adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~BattlefieldAllUnits|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Roam|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result: 2212/2212 passed.

2026-06-28 backend full after battlefield all-units RULE_TEXT Roam official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result: 8855/8855 passed.

2026-06-26 source same-location static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSourceSameLocationStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 source same-location static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 28/28 passed.

2026-06-26 source same-location static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceSameLocation|FullyQualifiedName~ReliableSiegeDog|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2101/2101 passed.

2026-06-26 backend full after source same-location static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8720/8720 passed.

2026-06-26 same-battlefield boon count-to-source static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSameBattlefieldBoonCountStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 same-battlefield boon count-to-source static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 29/29 passed.

2026-06-26 same-battlefield boon count-to-source static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Sett|FullyQualifiedName~Boon|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2214/2214 passed.

2026-06-26 backend full after same-battlefield boon count-to-source static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8721/8721 passed.

2026-06-26 same-battlefield other-friendly filtered static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSameBattlefieldOtherFriendlyFilteredStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 same-battlefield other-friendly filtered static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 30/30 passed.

2026-06-26 same-battlefield other-friendly filtered static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LeeSin|FullyQualifiedName~Boon|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2206/2206 passed.

2026-06-26 backend full after same-battlefield other-friendly filtered static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8722/8722 passed.

2026-06-26 source-lone-battle static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSourceLoneBattleStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 source-lone-battle static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 31/31 passed.

2026-06-26 source-lone-battle static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSourceLoneBattleStaticAura|FullyQualifiedName~Waterbender|FullyQualifiedName~SourceLoneBattle|FullyQualifiedName~SourceCombat|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~WatchfulSentinel" --nologo
```

Result: 2116/2116 passed.

2026-06-26 backend full after source-lone-battle static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8723/8723 passed.

2026-06-26 friendly single-defender static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesFriendlySingleDefenderStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 friendly single-defender static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 32/32 passed.

2026-06-26 friendly single-defender static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesFriendlySingleDefenderStaticAura|FullyQualifiedName~MasterYi|FullyQualifiedName~FriendlySingleDefender|FullyQualifiedName~FriendlySingleDefending|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~DemaciaEnvoy" --nologo
```

Result: 2108/2108 passed.

2026-06-26 backend full after friendly single-defender static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8724/8724 passed.

2026-06-26 source-object filtered static-power official-deck full-game focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesWiseElderSourceObjectFilteredStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 source-object filtered static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 34/34 passed.

2026-06-26 source-object filtered static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesWiseElderSourceObjectFilteredStaticAura|FullyQualifiedName~WiseElder|FullyQualifiedName~SourceObjectFiltered|FullyQualifiedName~Boon|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery|FullyQualifiedName~ArenaRookie" --nologo
```

Result: 2207/2207 passed.

2026-06-26 backend full after source-object filtered static-power official-deck full-game evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8726/8726 passed.

2026-06-26 same-battlefield ephemeral count-to-source static-power official-deck midgame focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesPetalPixieSameBattlefieldEphemeralCountStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 same-battlefield ephemeral count-to-source static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 36/36 passed.

2026-06-26 same-battlefield ephemeral count-to-source static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesPetalPixieSameBattlefieldEphemeralCountStaticAura|FullyQualifiedName~PetalPixie|FullyQualifiedName~SameBattlefieldFriendlyFiltered|FullyQualifiedName~Ephemeral|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2139/2139 passed.

2026-06-26 backend full after same-battlefield ephemeral count-to-source static-power official-deck midgame evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8728/8728 passed.

2026-06-26 friendly-token static-power official-deck midgame focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSoulShepherdFriendlyTokenStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 friendly-token static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 37/37 passed.

2026-06-26 friendly-token static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSoulShepherdFriendlyTokenStaticAura|FullyQualifiedName~SoulShepherd|FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~UnitToken|FullyQualifiedName~Token|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2203/2203 passed.

2026-06-26 backend full after friendly-token static-power official-deck midgame evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8729/8729 passed.

2026-06-26 friendly-mechanical static-power official-deck midgame focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesRumbleFriendlyMechanicalStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-26 friendly-mechanical static-power official-deck FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 38/38 passed.

2026-06-26 friendly-mechanical static-power official-deck adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesRumbleFriendlyMechanicalStaticAura|FullyQualifiedName~Rumble|FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2120/2120 passed.

2026-06-26 backend full after friendly-mechanical static-power official-deck midgame evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8730/8730 passed.

2026-06-26 battlefield isolated-defender keyword-modifier focused/projection official-deck check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldIsolatedDefenderKeywordModifier" --nologo
```

Result: 3/3 passed.

2026-06-26 battlefield isolated-defender keyword-modifier FullGameEndToEnd check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 39/39 passed.

2026-06-26 battlefield isolated-defender keyword-modifier adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldIsolatedDefenderKeywordModifier|FullyQualifiedName~BattlefieldIsolated|FullyQualifiedName~ForbiddenWasteland|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~Steadfast|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2111/2111 passed.

2026-06-26 backend full after battlefield isolated-defender keyword-modifier evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8733/8733 passed.

2026-06-28 Darius same-battlefield static-aura focused official-deck check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesDariusSameBattlefieldStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-28 same-battlefield static-aura focused check after Darius official-deck replay:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSameBattlefieldStaticAura|FullyQualifiedName~OfficialDeckMidgameAppliesDariusSameBattlefieldStaticAura|FullyQualifiedName~SameBattlefieldOtherFriendlyStaticPowerCardRowTests" --nologo
```

Result: 7/7 passed.

2026-06-28 FullGameEndToEnd check after Darius same-battlefield static-aura replay:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 89/89 passed.

2026-06-28 Darius same-battlefield static-aura adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Darius|FullyQualifiedName~SameBattlefieldOtherFriendly|FullyQualifiedName~SameBattlefieldStaticAura|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2166/2166 passed.

2026-06-28 backend full after Darius same-battlefield static-aura replay:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8853/8853 passed.

2026-06-28 Ornn friendly-equipment static-aura focused official-deck check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesOrnnFriendlyEquipmentStaticAura" --nologo
```

Result: 1/1 passed.

2026-06-28 Ornn friendly-equipment static-aura focused representative check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesOrnnFriendlyEquipmentStaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~LayerEngine" --nologo
```

Result: 64/64 passed.

2026-06-28 FullGameEndToEnd check after Ornn friendly-equipment static-aura replay:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 90/90 passed.

2026-06-28 Ornn friendly-equipment static-aura adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Ornn|FullyQualifiedName~FriendlyEquipment|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2200/2200 passed.

2026-06-28 backend full after Ornn friendly-equipment static-aura replay:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8854/8854 passed.

Earlier backend full check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8602/8602 passed.

Latest same-location source threshold adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~ReliableSiegeDog"
```

Result: 406/406 passed.

Latest Ornn friendly-equipment runtime bridge removal adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~LayerEngineTimestampDependency|FullyQualifiedName~ContinuousEffect"
```

Result: 417/417 passed.

Latest Ornn friendly-equipment runtime bridge removal MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Latest result: 8954/8954 passed.

2026-06-29 source-object level static-power focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceObjectLevelPowerStaticAuraTests|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives"
```

Result: 5/5 passed.

2026-06-29 source-object level static-power adjacent / recovery check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceObjectLevelPower|FullyQualifiedName~SourceObjectPower|FullyQualifiedName~SourceObjectFiltered|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~Experience|FullyQualifiedName~MatchRecovery"
```

Result: 2149/2149 passed.

2026-06-29 backend full after source-object level static-power:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8954/8954 passed.

2026-06-29 Crystalhand Hunter source-object level static-power official-deck focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesCrystalhandHunterSourceObjectLevelStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result: 1/1 passed.

2026-06-29 Crystalhand Hunter source-object level static-power official-deck adjacent / recovery check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesCrystalhandHunterSourceObjectLevelStaticAura|FullyQualifiedName~SourceObjectLevelPower|FullyQualifiedName~SourceObjectPower|FullyQualifiedName~SourceObjectFiltered|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~Experience|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result: 2243/2243 passed.

2026-06-29 backend full after Crystalhand Hunter source-object level official-deck evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8955/8955 passed.

2026-06-29 Targon Seer source-object level static-power official-deck focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesTargonSeerSourceObjectLevelStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash"
```

Result: 1/1 passed.

2026-06-29 Targon Seer source-object level static-power official-deck adjacent / recovery check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~OfficialDeckMidgameAppliesTargonSeerSourceObjectLevelStaticAura|FullyQualifiedName~SourceObjectLevelPower|FullyQualifiedName~SourceObjectPower|FullyQualifiedName~SourceObjectFiltered|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~Experience|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery"
```

Result: 2244/2244 passed.

2026-06-29 backend full after Targon Seer source-object level official-deck evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8956/8956 passed.

2026-06-29 Flameclaw level-gated active-entry + source-object level static-power official-deck focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceUnitLevelActiveEntryStaticAbility|FullyQualifiedName~OfficialDeckMidgameResolvesFlameclawLevelActiveEntryStaticAura"
```

Result: 4/4 passed.

2026-06-29 Flameclaw active-entry / source-object level static-power adjacent / recovery check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceUnitLevelActiveEntry|FullyQualifiedName~SourceUnitEnterReady|FullyQualifiedName~ActiveEntry|FullyQualifiedName~StaticAbility|FullyQualifiedName~StaticAura|FullyQualifiedName~SourceObjectLevelPower|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result: 2501/2501 passed.

2026-06-29 backend full after Flameclaw source-object level official-deck evidence:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8971/8971 passed.
