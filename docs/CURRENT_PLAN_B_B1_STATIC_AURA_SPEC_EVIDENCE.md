# Plan B B1 Static Aura Spec Evidence

更新时间：2026-06-30

## Evidence Summary

This evidence records the current B1 static-aura data-driven slices.

## 2026-07-01 Supplement Evidence: Public-Field Friendly RULE_TEXT Keyword Aura Scope Router

- `StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura` is now the shared predicate for public-field friendly RULE_TEXT keyword auras whose target scope is other friendly units or friendly filtered units.
- `CoreRuleEngine.ResolveFriendlyFilteredUnitsKeywordBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(sourceState.CardNo)` and uses the shared predicate instead of filtering by `StaticAuraKinds.FriendlyFilteredUnitsKeyword` and `StaticAuraKinds.OtherFriendlyUnitsKeyword`.
- `CoreRuleEngine` resource-keyword amount and static-granted keyword checks now consume the same predicate.
- `MatchSession.BuildFriendlyFilteredUnitsKeywordAuraEffects` and `BuildOtherFriendlyUnitsKeywordAuraEffects` now enumerate `StaticAuraSpecRules.GetStaticAuras(source.CardNo)` and project matching keyword auras from their `StaticAuraSpec` target/participant scopes instead of kind-specific filters.
- `MatchSession` prompt dynamic keyword and Spellshield tax checks now consume the same predicate.
- Existing public effect id shapes remain compatible: `RULE_TEXT:FRIENDLY_FILTERED_UNITS_KEYWORD` and `RULE_TEXT:OTHER_FRIENDLY_UNITS_KEYWORD`.
- `StaticAuraSpecRules.TryGetFriendlyFilteredUnitsKeywordAura` and `TryGetOtherFriendlyUnitsKeywordAura` have been removed from the engine helper surface.
- `BattlefieldStaticAuraSpecRoutingGuardTests.PublicFieldFriendlyKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing the removed helpers or direct `StaticAuraKinds.FriendlyFilteredUnitsKeyword` / `StaticAuraKinds.OtherFriendlyUnitsKeyword` runtime filtering in CoreRuleEngine or MatchSession.
- Validation passed: baseline backend full conformance 9067/9067; red/green focused guard 1/1; public-field keyword focused regression 51/51; StaticAura / StaticKeyword / Roam / Spellshield / Predict / FullGameEndToEnd / MatchRecovery adjacent 2349/2349; backend full conformance 9068/9068.
- Non-closure: this does not finish battlefield isolated-defender keyword modifier routing, battlefield all-units granted-keyword shared query cleanup, complete B2 keyword breadth, complete LayerEngine timestamp/order behavior, full P0, or READY.

## 2026-07-01 Supplement Evidence: Same-Battlefield Other-Friendly RULE_TEXT Keyword Aura Scope Router

- `StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyKeywordStaticAura` is now the shared predicate for same-battlefield other-friendly RULE_TEXT keyword auras whose target scope is same-battlefield other friendly units and whose participant scope is same-battlefield other friendly public units.
- `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsKeywordBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(sourceState.CardNo)` and takes the max matching granted combat keyword amount instead of calling `StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura`.
- `CoreRuleEngine` resource-keyword tax calculation now consumes the same shared predicate for same-battlefield other-friendly keyword grants.
- `MatchSession.BuildSameBattlefieldOtherFriendlyUnitsKeywordAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(source.CardNo)` and projects matching same-battlefield other-friendly keyword auras from their `StaticAuraSpec` scopes instead of a kind-specific helper.
- `MatchSession` prompt resource-keyword tax calculation now consumes the same shared predicate.
- Existing public effect id shape remains compatible: `RULE_TEXT:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD`.
- `StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura` has been removed from the engine helper surface.
- `BattlefieldStaticAuraSpecRoutingGuardTests.SameBattlefieldOtherFriendlyKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing the kind-specific helper or `StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword` runtime filtering in CoreRuleEngine or MatchSession.
- Validation passed: baseline backend full conformance 9066/9066; red/green focused guard 1/1; same-battlefield keyword focused regression 9/9; StaticKeyword / StaticAura / SameBattlefield / Spellshield / FullGameEndToEnd / MatchRecovery adjacent 2319/2319; backend full conformance 9067/9067.
- Non-closure: this does not finish public-field RULE_TEXT keyword routing, battlefield isolated-defender keyword modifier routing, complete B2 keyword breadth, complete LayerEngine timestamp/order behavior, full P0, or READY.

## 2026-07-01 Supplement Evidence: Source Participant-Count POWER Aura Scope Router

- `StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura` is now the shared predicate for source-object POWER static auras whose participant scope is friendly public field equipment, same-battlefield friendly filtered public units, or same-location other friendly public units.
- `CoreRuleEngine.ResolveSourceParticipantCountPowerStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)` and sums matching non-equipment participant-count `PowerDeltaPerParticipant` values instead of calling separate same-battlefield filtered count and same-location threshold POWER resolvers.
- `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` and source-unit entry power now derive Ornn-style friendly-equipment count-to-source power from the same shared predicate while preserving the persistent source-power recompute path.
- `MatchSession.BuildSourceParticipantCountPowerStaticAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)` and projects matching source participant-count POWER static auras from their `StaticAuraSpec` participant scopes instead of separate projection builders.
- Existing recovery-sensitive metadata remains compatible for the current representatives: `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER`, `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER`, and `SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER` still project the same effect id prefixes, source paths, conditions and lifecycle strings.
- `BattlefieldStaticAuraSpecRoutingGuardTests.SourceParticipantCountPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing direct `StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura` / `TryGetSameBattlefieldFriendlyFilteredUnitCountToSourcePowerAura` / `TryGetSourceSameLocationOtherFriendlyUnitPowerAura` calls in CoreRuleEngine or MatchSession source participant-count POWER execution/projection routing.
- Validation passed: red/green focused guard 1/1; source participant-count focused regression 70/70; StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent 2246/2246; backend full conformance 9065/9065.
- Non-closure: this does not finish friendly single-defender POWER, source-object RULE_TEXT keyword routing, complete LayerEngine timestamp/order behavior, full B1, P0, or READY.

## 2026-07-01 Supplement Evidence: Source Battle-State POWER Aura Scope Router

- `StaticAuraSpecRules.IsSourceBattleStatePowerStaticAura` is now the shared predicate for source-object POWER static auras whose participant scope is current attackers, all current battle participants, or ready enemy units at the current battlefield.
- `CoreRuleEngine.ResolveSourceBattleStatePowerStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)` and sums matching `PowerDeltaPerParticipant` values instead of calling separate source-attacking-with-another, source-lone-battle, and source-attacking-ready-enemy POWER resolvers.
- `MatchSession.BuildSourceBattleStatePowerStaticAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)` and projects matching source battle-state POWER static auras from their `StaticAuraSpec` participant scopes instead of separate projection builders.
- Existing recovery-sensitive metadata remains compatible for the current representatives: `SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `SOURCE_LONE_BATTLE_POWER`, and `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` still project the same effect id prefixes, source paths, conditions and lifecycle strings.
- `BattlefieldStaticAuraSpecRoutingGuardTests.SourceBattleStatePowerStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing direct `StaticAuraSpecRules.TryGetSourceAttackingWithAnotherUnitPowerAura` / `TryGetSourceLoneBattlePowerAura` / `TryGetSourceAttackingReadyEnemyUnitPowerAura` calls in CoreRuleEngine or MatchSession source battle-state POWER execution/projection routing.
- Validation passed: red/green focused guard 1/1; source-combat focused regression 16/16; StaticAura / StaticPower / SourceCombat / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent 2219/2219; backend full conformance 9064/9064.
- Non-closure: this does not finish source-object RULE_TEXT keyword routing, count-to-source POWER families, friendly-equipment count-to-source power, complete LayerEngine timestamp/order behavior, full B1, P0, or READY.

## 2026-06-30 Supplement Evidence: Source-Object POWER Aura Scope Router

- `StaticAuraSpecRules.IsSourceObjectPowerStaticAura` is now the shared predicate for source-object POWER static auras whose target/participant scope is the source object itself.
- `CoreRuleEngine.ResolveSourceObjectPowerStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)` and sums matching `PowerDeltaPerParticipant` values instead of calling separate source-object filtered and source-object level/experience POWER resolvers.
- `MatchSession.BuildSourceObjectPowerStaticAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(cardObject.CardNo)` and projects matching source-object POWER static auras from their `StaticAuraSpec` scopes instead of separate filtered and level/experience projection builders.
- Existing recovery-sensitive metadata remains compatible for the current representatives: `SOURCE_OBJECT_FILTERED_POWER` and `SOURCE_OBJECT_POWER` still project the same effect id prefixes, source paths, conditions and lifecycle strings.
- `BattlefieldStaticAuraSpecRoutingGuardTests.SourceObjectPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing direct `StaticAuraSpecRules.TryGetSourceObjectPowerAura` / `TryGetSourceObjectFilteredPowerAura` calls in CoreRuleEngine or MatchSession source-object POWER execution/projection routing.
- Validation passed: red/green focused guard 1/1; source-object focused regression 16/16; SourceObject / StaticAura / StaticPower / ContinuousEffect / Experience / FullGameEndToEnd / MatchRecovery adjacent 2266/2266; backend full conformance 9063/9063.
- Non-closure: this does not finish source-object RULE_TEXT keyword routing, conditional battle-state source-object POWER families, friendly-equipment count-to-source power, complete LayerEngine timestamp/order behavior, full B1, P0, or READY.

## 2026-06-30 Supplement Evidence: Public-Field Friendly POWER Aura Scope Router

- `StaticAuraSpecRules.IsPublicFieldFriendlyPowerStaticAura` is now the shared predicate for public-field friendly POWER static auras whose target/participant scope is friendly public units, other friendly public units, or friendly filtered public units.
- `CoreRuleEngine.ResolvePublicFieldFriendlyPowerStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(sourceState.CardNo)` and sums matching `PowerDeltaPerParticipant` values instead of calling separate friendly-units, other-friendly-units, and friendly-filtered-units POWER resolvers.
- `MatchSession.BuildPublicFieldFriendlyPowerStaticAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(source.CardNo)` and projects matching public-field friendly POWER static auras from their `StaticAuraSpec` scopes instead of separate projection builders.
- Existing recovery-sensitive metadata remains compatible for the current representatives: `FRIENDLY_UNITS_POWER`, `OTHER_FRIENDLY_UNITS_POWER`, and `FRIENDLY_FILTERED_UNITS_POWER` still project the same effect id prefixes, source paths, conditions and lifecycle strings.
- `BattlefieldStaticAuraSpecRoutingGuardTests.PublicFieldFriendlyPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing direct `StaticAuraSpecRules.TryGetOtherFriendlyUnitsPowerAura` / `TryGetFriendlyFilteredUnitsPowerAura` calls in CoreRuleEngine or MatchSession public-field friendly POWER execution/projection routing.
- Validation passed: red/green focused guard 1/1; public-field static-aura focused regression 346/346; StaticAura / StaticPower / SameBattlefield / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent 2218/2218; backend full conformance 9062/9062.
- Non-closure: this does not finish same-battlefield other-friendly RULE_TEXT keyword routing, other public-field RULE_TEXT keyword routing, object-source aura families, complete LayerEngine timestamp/order behavior, full B1, P0, or READY.

## 2026-06-30 Supplement Evidence: Same-Battlefield Other-Friendly POWER Aura Scope Router

- `StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyPowerStaticAura` is now the shared predicate for same-battlefield other-friendly POWER static auras whose target/participant scope is either all other friendly public units at the source battlefield or filtered other friendly public units at the source battlefield.
- `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyPowerStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(sourceState.CardNo)` and sums matching `PowerDeltaPerParticipant` values instead of calling separate normal and filtered same-battlefield other-friendly POWER helper selectors.
- `MatchSession.BuildSameBattlefieldOtherFriendlyPowerStaticAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(source.CardNo)` and projects matching same-battlefield other-friendly POWER static auras from their `StaticAuraSpec` scopes instead of separate normal and filtered projection builders.
- Existing recovery-sensitive metadata remains compatible for the current representatives: `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` still projects the same effect id prefix, source path, condition and lifecycle; `SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER` does the same for filtered same-battlefield other-friendly static power.
- `BattlefieldStaticAuraSpecRoutingGuardTests.SameBattlefieldOtherFriendlyPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing `StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsPowerAura` / `TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura` calls in CoreRuleEngine or MatchSession same-battlefield other-friendly POWER execution/projection routing.
- Validation passed: red/green focused guard 1/1; same-battlefield static-power focused regression 12/12; StaticAura / StaticPower / SameBattlefield / FullGameEndToEnd / MatchRecovery adjacent 2217/2217; backend full conformance 9061/9061.
- Non-closure: this does not finish same-battlefield other-friendly RULE_TEXT keyword routing, non-local other-friendly POWER routing, friendly-filtered/friendly-units routing, complete LayerEngine timestamp/order behavior, full B1, P0, or READY.

## 2026-06-30 Supplement Evidence: Battlefield RULE_TEXT Keyword Aura Scope Router

- `StaticAuraSpecRules.IsBattlefieldKeywordStaticAura` is now the shared predicate for battlefield RULE_TEXT keyword auras whose target/participant scope is either all public units at the source battlefield or filtered public units at the source battlefield.
- `StaticAuraSpecRules.HasBattlefieldKeywordStaticAura(cardNo)` is now the shared battlefield-rule recognition query for those keyword aura shapes.
- `StaticAuraSpecRules.TryGetBattlefieldAllUnitsGrantedKeywordAura(cardNo, keyword, out aura)` now uses the shared battlefield keyword aura predicate before checking all-units target scope and the requested granted keyword.
- `CoreRuleEngine.ResolveBattlefieldKeywordStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(battlefieldState.CardNo)` and takes the max matching granted combat keyword amount instead of calling separate all-units and filtered-units keyword helper selectors.
- `MatchSession.BuildBattlefieldKeywordAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo)` and projects matching battlefield keyword auras from their `StaticAuraSpec` scopes instead of separate all-units and filtered-units projection builders.
- Existing public effect id shapes remain compatible: `RULE_TEXT:BATTLEFIELD_ALL_UNITS_KEYWORD` and `RULE_TEXT:BATTLEFIELD_FILTERED_UNITS_KEYWORD`.
- `BattlefieldStaticAuraSpecRoutingGuardTests.BattlefieldKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing direct `StaticAuraSpecRules.TryGetBattlefieldAllUnitsKeywordAura` / `TryGetBattlefieldFilteredUnitsKeywordAura` calls in CoreRuleEngine or MatchSession battlefield keyword execution/projection routing.
- Validation passed: red/green focused keyword guard 1/1; battlefield keyword / Roam focused regression 63/63; StaticAura / StaticKeyword / Battlefield / Roam / MoveUnit / FullGameEndToEnd / MatchRecovery adjacent 2329/2329; backend full conformance 9060/9060.
- Non-closure: this does not finish other RULE_TEXT aura families, full Roam/movement timing breadth, complete B2 keyword breadth, complete LayerEngine timestamp/order behavior, full B1/B2, P0, or READY.

## 2026-06-30 Supplement Evidence: Battlefield POWER Static Aura Scope Router

- `StaticAuraSpecRules.IsBattlefieldPowerStaticAura` is now the shared predicate for battlefield POWER static auras whose target/participant scope is either all public units at the source battlefield or filtered public units at the source battlefield.
- `StaticAuraSpecRules.HasBattlefieldPowerStaticAura(cardNo)` is now the shared battlefield-rule recognition query for those POWER aura shapes.
- `CoreRuleEngine.ResolveBattlefieldPowerStaticAuraBonus` now enumerates `StaticAuraSpecRules.GetStaticAuras(battlefieldState.CardNo)` and sums matching `PowerDeltaPerParticipant` values instead of calling separate all-units and filtered-units POWER helper selectors.
- `MatchSession.BuildBattlefieldPowerStaticAuraEffects` now enumerates `StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo)` and projects matching battlefield POWER static auras from their `StaticAuraSpec` scopes instead of separate all-units and filtered-units projection builders.
- Existing recovery-sensitive metadata remains compatible for the current representatives: `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` still projects the same effect id prefix, source path, condition and lifecycle; `BATTLEFIELD_FILTERED_UNITS_POWER` does the same for filtered battlefield static power.
- `BattlefieldStaticAuraSpecRoutingGuardTests.BattlefieldPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope` blocks reintroducing `StaticAuraSpecRules.TryGetBattlefieldAllUnitsPowerAura` / `TryGetBattlefieldFilteredUnitsPowerAura` calls in CoreRuleEngine or MatchSession battlefield POWER execution/projection routing.
- Validation passed: red/green focused guard 1/1; battlefield/static-aura focused behavior regression 34/34; StaticAura / StaticPower / Battlefield / FullGameEndToEnd / MatchRecovery adjacent 2843/2843; backend full conformance 9059/9059.
- Non-closure: this does not finish battlefield RULE_TEXT aura routing, object-source aura routing, friendly-equipment source-power routing, complete LayerEngine timestamp/order behavior, full B1, P0, or READY.

## 2026-06-30 Supplement Evidence: Shared Battlefield All-Units Keyword Aura Query

- `StaticAuraSpecRules.TryGetBattlefieldAllUnitsGrantedKeywordAura(cardNo, keyword, out aura)` is now the shared engine query for battlefield all-units `RULE_TEXT` keyword auras.
- `CoreRuleEngine.HasBattlefieldStaticRoamPermission` and `MatchSession.HasMoveUnitPromptRoamPermission` now consume that shared query for `MoveUnitRoamKeyword`.
- `CoreRuleEngine.HasImplementedBattlefieldRuleSpec` and `MatchSession.IsBattlefieldCardObject` now also recognize Wind Hill-style battlefield all-units keyword sources through the shared query.
- The duplicated private `BattlefieldSourceGrantsRoam` helpers were removed from `CoreRuleEngine` and `MatchSession`.
- `CardCatalogBaselineTests.StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList` now blocks reintroducing `BattlefieldSourceGrantsRoam` and requires `StaticAuraSpecRules.TryGetBattlefieldAllUnitsGrantedKeywordAura`.
- Validation passed: focused guard red/green 1/1; StaticAura / BattlefieldStaticRoam / BattlefieldAllUnits / Roam / MoveUnit / CardCatalogBaseline / MatchRecovery adjacent 2490/2490; backend full conformance 9049/9049.
- Non-closure: this does not expand complete Roam timing, movement lifecycle, full battlefield keyword aura breadth, B1/B2 completion, frontend validation, or READY.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` defines `StaticAuraSpec`, `StaticAuraKinds`, `StaticAuraTargetScopes`, and `StaticAuraParticipantScopes`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses the current representative static-aura text patterns, including battlefield all-units power, battlefield all-units keyword, battlefield-filtered, same-battlefield friendly-filtered count-to-source, same-battlefield boon count-to-source, same-location other-friendly source threshold, friendly single-defender combat power, experience-gated friendly-unit power, source-object level power, source-object combat power, same-battlefield other-friendly, same-battlefield friendly-filtered, non-local other-friendly, and friendly-filtered unit power / keyword auras.
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` exposes parsed static auras through `BehaviorSpec.StaticAuras`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Ornn, Tifarian Training Grounds, Brush, Petal Pixie, Sett, Reliable Siege Dog, Master Yi intro, Master Yi level, source-object level power cards, Scarlet Pigeon, Waterbender, Dune Drake, Soul Shepherd, Rumble, Lee Sin, Blackflame Altar, Forbidden Wasteland, Wind Hill, and Aerie Head Fan representative static-aura specs, plus false-positive guards for Boon-granting text and Brush reminder text on Ivern's legend.
- `StaticAuraSpec.RequiredParticipantCount` records threshold-style conditions where at least N participants enable a fixed `PowerDeltaPerParticipant` rather than multiplying the power delta by all participants.
- `StaticAuraSpec.RequiredPlayerExperience` records level/experience thresholds for static auras such as Master Yi level's `{{等级6>}}` friendly-unit power and source-object `{{等级N>}} 我获得{{S}}+X` power.

Engine projection:

- `src/Riftbound.Engine/StaticAuraSpecRules.cs` builds a cached map from official card catalog `BehaviorSpec.StaticAuras`.
- `src/Riftbound.Engine/StaticAuraSpecRules.cs` evaluates `TargetFilter` values including single tags, card names, unit-token predicates, and `ANY:` filter groups.
- `src/Riftbound.Engine/MatchSession.cs` no longer declares `ContinuousEffectStaticAuraCards`; object and battlefield `STATIC_AURA` projections resolve via `StaticAuraSpecRules`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves implemented static-aura power and keyword bonuses from `BehaviorSpec.StaticAuras`, including battlefield all-units keyword, battlefield-filtered, same-battlefield friendly-filtered count-to-source, same-location other-friendly source threshold, friendly single-defender combat power, experience-gated friendly-unit power, friendly-filtered, and same-battlefield friendly-filtered target filters.
- `src/Riftbound.Engine/CoreRuleEngine.cs` no longer uses the old `HasMasterYiSingleDefenderBonus` / `ResolveMasterYiLevelLegendPowerBonus` combat-power special paths; Master Yi intro and level power bonuses are resolved by enumerating public static-aura sources and reading `BehaviorSpec.StaticAuras`.
- `src/Riftbound.Engine/MatchSession.cs` projects the Master Yi level legend aura from the legend zone as `FRIENDLY_UNITS_POWER` only when the controller meets `StaticAuraSpec.RequiredPlayerExperience`.
- `src/Riftbound.Engine/MatchSession.cs` projects source-object combat static auras from the active battle state for `SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `SOURCE_LONE_BATTLE_POWER`, and `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER`.
- `src/Riftbound.Engine/MatchSession.cs` now excludes standby battle participants from source-combat participant evaluation and from static-aura participant / dependency metadata.
- `src/Riftbound.Engine/CoreRuleEngine.cs` now excludes standby source units before applying source-combat static-power bonuses in `DECLARE_BATTLE` damage calculation.
- `src/Riftbound.Engine/CoreRuleEngine.cs` exposes the lone-battle representative through the generic `ResolveSourceLoneBattlePowerBonus` source path rather than a Waterbender-named helper.
- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves `SOURCE_OBJECT_FILTERED_POWER` through `ResolveSourceObjectFilteredPowerBonus`; `WiseElderSourceObjectFilteredPowerTests.WiseElderBoonStaticPowerAppliesToBattleDamage` proves the spec-driven source-object filtered static power contributes to real battle damage.
- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves `SOURCE_OBJECT_POWER` through `ResolveSourceObjectPowerBonus`; `MatchSession.TryBuildSourceObjectPowerStaticAuraEffect` projects the same source-object level power only when the controller satisfies `StaticAuraSpec.RequiredPlayerExperience`.
- `src/Riftbound.Engine/StaticAuraSpecRules.cs` includes a generic materialized-level-power guard so legacy source-unit entry paths that already stored the parsed level power in `CardObjectState.Power` do not double-count the same `BehaviorSpec.StaticAuras` delta during combat.
- `src/Riftbound.Engine/CoreRuleEngine.cs` now resolves Ornn-style friendly-equipment count-to-source power recompute and source-unit entry power through `StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura` and `StaticAuraSpec.PowerDeltaPerParticipant`; `CardBehaviorDefinition.AddsFriendlyFieldEquipmentCountToSourceUnitPower` has been deleted.
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` now marks the friendly-equipment static-power representative boundary from `BehaviorSpec.StaticAuras` rather than a registry runtime flag.
- `src/Riftbound.Engine/MatchSession.cs` now excludes standby sources from Ornn-style friendly-equipment static-power projection, and `src/Riftbound.Engine/CoreRuleEngine.cs` excludes standby sources before friendly-equipment source-power recompute.
- `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` grant battlefield static `ROAM` from `StaticAuraSpec.Kind=BATTLEFIELD_ALL_UNITS_KEYWORD` + `GrantedKeyword=游走`; the old `BattlefieldStaticRoamCardNo` / `IsBattlefieldStaticRoamCardNo` branches are removed.
- `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` grant enemy spell target `法盾` tax from `StaticAuraSpec.Kind=SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` + `GrantedKeyword=法盾` when a face-up public source and another friendly unit share a battlefield.
- `src/Riftbound.Engine/MatchSession.cs` projects `BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER` RULE_TEXT continuous effects from `BehaviorSpec.StaticAuras` when the active battle has exactly one public defender at the source battlefield; the effect is absent when the defender is not isolated.
- `src/Riftbound.Engine/MatchSession.cs` now requires battlefield keyword aura sources to be non-face-down before projecting `BATTLEFIELD_ALL_UNITS_KEYWORD` or `BATTLEFIELD_FILTERED_UNITS_KEYWORD` RULE_TEXT continuous effects.
- `src/Riftbound.Engine/CoreRuleEngine.cs` now requires battlefield keyword aura sources to be non-face-down before applying battlefield all-units / filtered-units combat keyword bonuses or static Roam permission.
- `src/Riftbound.Engine/MatchSession.cs` now requires battlefield static-power aura sources to be non-face-down before projecting `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` or `BATTLEFIELD_FILTERED_UNITS_POWER` `STATIC_AURA` continuous effects.
- `src/Riftbound.Engine/CoreRuleEngine.cs` now requires battlefield static-power aura sources to be non-face-down before applying battlefield all-units / filtered-units combat power bonuses.
- `src/Riftbound.Engine/MatchSession.cs` now excludes face-down / standby units from all-units battlefield static-aura participant projection.
- `src/Riftbound.Engine/CoreRuleEngine.cs` now excludes face-down / standby target units before applying all-units battlefield static-power bonuses.
- `src/Riftbound.Engine/MatchSession.cs` now excludes standby sources and standby participant targets from non-local other-friendly static-power projection, and `src/Riftbound.Engine/CoreRuleEngine.cs` excludes standby other-friendly static-power sources before combat recompute.
- `src/Riftbound.Engine/MatchSession.cs` now excludes standby sources and standby participant targets from same-battlefield other-friendly static-power projection, and `src/Riftbound.Engine/CoreRuleEngine.cs` excludes standby same-battlefield other-friendly source/target units before combat recompute.
- `src/Riftbound.Engine/CoreRuleEngine.cs` no longer declares `IsPetalPixieCardNo`; the Petal Pixie battle power representative is now spec-driven.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` includes a source-level guard that rejects reintroducing `ContinuousEffectStaticAuraCards` in `MatchSession`, rejects Ornn friendly-equipment static power reintroducing the old registry runtime bridge in `CoreRuleEngine`, `CardEquipmentKeywordRules`, or `CardBehaviorRegistry`, and rejects reintroducing Master Yi-specific combat-power helpers.
- `tests/Riftbound.ConformanceTests/MasterYiLegendStaticAuraSpecTests.cs` verifies that the level-6 Master Yi aura projects from a legend-zone source into both player snapshots and stays absent below six experience.
- `tests/Riftbound.ConformanceTests/SourceObjectLevelPowerStaticAuraTests.cs` verifies Crystalhand Hunter's level-6 source-object power projection, below-threshold absence, and real battle damage `staticPowerBonus=1`.
- `tests/Riftbound.ConformanceTests/StaticAuraStackingAndModifierTests.cs` verifies that Scarlet Pigeon's source-combat static aura, Baron Nashor's other-friendly static aura, and an until-end-of-turn power modifier stack additively in continuous-effect projection and real `DECLARE_BATTLE` combat damage.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the optional `requiredParticipantCount` and `requiredPlayerExperience` protocol fields for static-aura catalog payloads.

Recovery:

- `src/Riftbound.Engine/MatchRecovery.cs` validates object and battlefield static-aura source cards against the `BehaviorSpec` aura surface, including legend-source friendly-unit power auras.
- `src/Riftbound.Engine/MatchRecovery.cs` validates `SOURCE_OBJECT_POWER` effect ids, source paths, condition/lifecycle metadata, source-card specs, and `powerDelta` from `StaticAuraSpecRules`.
- `src/Riftbound.Engine/MatchRecovery.cs` validates same-location source threshold object static auras with fixed power delta even when multiple participant object ids satisfy the threshold.
- `src/Riftbound.Engine/MatchRecovery.cs` validates source-object combat static-aura durations, effect ids, source paths, conditions, lifecycle strings, source cards, and fixed `powerDelta` scalars through `StaticAuraSpecRules`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` updates the source-card drift expectation to the spec-driven diagnostic.
- `tests/Riftbound.ConformanceTests/SameBattlefieldOtherFriendlyStaticPowerCardRowTests.cs` binds all current official `此处的其他友方单位获得{{S}}+1` rows to the same spec-driven projection and combat-damage path, covering `OGS·013/024`, `SFD·236/221`, `SFD·236*/221`, `OGN·243/298`, and `OGN·243a/298`.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGS·013/024` Garen's same-battlefield other-friendly static aura through a legal official Poppy deck route: Garen and `UNL-092/219` Demacia Envoy are played and moved through server prompts, the projected `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` effect targets the Envoy, real battle damage records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now also carries `SFD·236/221` Darius's same-battlefield other-friendly static aura through a legal official Darius deck route with `OGN·253/298` legend and `OGN·243/298` champion: Darius and `SFD·006/221` Aggressive Dragonhound are played and moved through server prompts, the projected `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE` effect targets the Dragonhound, real battle damage records `basePower=3`, `staticPowerBonus=1`, `combatPower=4`, and `damage=4`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `SFD·085/221` Ornn's friendly-equipment count-to-source static aura through a legal official Rumble deck route with official `SFD·022/221` Long Sword as the friendly public field equipment participant: Ornn is played and moved through server prompts, the projected `FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER` effect targets Ornn itself with `PowerDelta=1`, `BasePower=4`, `EffectivePower=5`, real battle damage records recomputed `basePower=5`, no extra `staticPowerBonus`, `combatPower=5`, and `damage=5`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-147/219` Baron Nashor's non-local other-friendly static aura through a legal official Vex deck route: Baron Nashor is played to base, `UNL-057/219` Wildclaw Beastmaster is played and moved to a battlefield through server prompts, the projected `OTHER_FRIENDLY_UNITS_POWER` effect targets Wildclaw from the non-local source, real battle damage records `basePower=7`, `staticPowerBonus=2`, `combatPower=9`, and `damage=9`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-154/219` Scarlet Pigeon's source-combat static aura through a legal official Poppy deck route: Scarlet Pigeon and `UNL-092/219` Demacia Envoy are played and moved through server prompts, the server-authored `DECLARE_BATTLE` includes both as attackers, Scarlet Pigeon's `SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER` route contributes `staticPowerBonus=2` to real battle damage, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·131/298` Dune Drake's source-attacking-ready-enemy static aura through a legal official Poppy deck route: Dune Drake and an opposing ready defender are played and moved to the same battlefield through server prompts, the server-authored `DECLARE_BATTLE` has Dune Drake attacking alone, Dune Drake's `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` route contributes `staticPowerBonus=2` to real battle damage, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-076/219` Petal Pixie's same-battlefield ephemeral count-to-source static aura through a legal official Lillia deck midgame route: after legal official deck submission/opening is verified, Petal Pixie, official `UNL·T07` Faerie token and opposing `UNL-057/219` Wildclaw Beastmaster are staged at the same P1 battlefield, the projected `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` effect targets Petal Pixie itself with the Faerie token as participant, Petal Pixie's real battle damage records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-077/219` Soul Shepherd's friendly-token static aura through a legal official Lillia deck midgame route: after legal official deck submission/opening is verified, Soul Shepherd is staged in base while official `UNL·T02` Warhawk token and opposing `UNL-057/219` Wildclaw Beastmaster are staged at the same P1 battlefield, the projected `FRIENDLY_FILTERED_UNITS_POWER` effect targets the Warhawk token, the token's real battle damage records `basePower=1`, `staticPowerBonus=1`, `combatPower=2`, and `damage=2`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `SFD·089/221` Rumble's friendly-mechanical static aura through a legal official Rumble deck midgame route: after legal official deck submission/opening is verified, Rumble and opposing `OGN·096/298` Watchful Sentinel are staged at the same P1 battlefield, the projected `FRIENDLY_FILTERED_UNITS_POWER` effect targets Rumble itself through the official `机械` tag filter, Rumble's real battle damage records `basePower=4`, `staticPowerBonus=1`, `combatPower=5`, and `damage=5`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/BattlefieldIsolatedDefenderKeywordModifierProjectionTests.cs` and `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carry `UNL-210/219` Forbidden Wasteland's battlefield isolated-defender RULE_TEXT keyword modifier through direct projection tests and a legal official Vex / Rumble deck midgame route: single-defender projection grants LeBlanc `keyword=坚守` with `keywordBonus=-2`, two defenders suppress the effect, real defender damage records `basePower=4`, `combatPower=2`, and `damage=2`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·055/298` Waterbender's source-lone-battle static aura through a legal official Lillia deck route: Waterbender and opposing `OGN·096/298` Watchful Sentinel are played and moved through server prompts, the server-authored `DECLARE_BATTLE` has Waterbender attacking alone, Waterbender's `SOURCE_LONE_BATTLE_POWER` route contributes `staticPowerBonus=2` to real battle damage, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGS·019/024` Master Yi intro's friendly single-defender static aura through a legal official Master Yi intro deck route: `UNL-092/219` Demacia Envoy is played and moved through server prompts to the opposing battlefield, the server-authored `DECLARE_BATTLE` has Demacia Envoy as the only defender, Master Yi intro's `FRIENDLY_SINGLE_DEFENDING_UNIT_POWER` route contributes `staticPowerBonus=2` to real defender battle damage, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-191/219` Master Yi level's friendly-units static aura through a legal official Master Yi level deck route: the route starts at 5 experience, plays `UNL-092/219` Demacia Envoy through server prompts so its on-play text gains the sixth experience, projects `FRIENDLY_UNITS_POWER` from the legend source to the Envoy with `SourcePath=CoreRuleEngine.ResolveFriendlyUnitsPowerBonus`, records real attacker battle damage with `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·065/298` Wise Elder's source-object filtered static aura through a legal official Master Yi level green/orange deck route: Wise Elder is played and moved through server prompts, `OGN·136/298` Arena Rookie grants Wise Elder `{{增益}}` through a server-authored targeted `PLAY_CARD`, the projected `SOURCE_OBJECT_FILTERED_POWER` effect targets Wise Elder itself with `SourcePath=CoreRuleEngine.ResolveSourceObjectFilteredPowerBonus`, Wise Elder's real attacker battle damage records boon-adjusted `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-094/219` Crystalhand Hunter's source-object level static aura through a legal official Poppy deck route: the route starts at 6 experience, Crystalhand Hunter and opposing `OGN·096/298` Watchful Sentinel are played and moved through server prompts, the projected `SOURCE_OBJECT_POWER` effect targets Crystalhand Hunter itself with `SourcePath=CoreRuleEngine.ResolveSourceObjectPowerBonus`, Crystalhand Hunter's real attacker battle damage records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-098/219` Targon Seer's source-object level static aura through a legal official Poppy deck route: the route starts at 11 experience, Targon Seer and opposing `OGN·096/298` Watchful Sentinel are played and moved through server prompts, the projected `SOURCE_OBJECT_POWER` effect targets Targon Seer itself with `SourcePath=CoreRuleEngine.ResolveSourceObjectPowerBonus`, `PowerDelta=4`, and `RequiredPlayerExperience=11`, Targon Seer's real attacker battle damage records `basePower=6`, `staticPowerBonus=4`, `combatPower=10`, and `damage=10`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-016/219` Flameclaw's source-object level static aura through a legal official Jhin deck route while also exercising its level-gated source-unit active-entry spec: the route starts at 3 experience, Flameclaw is played directly to a battlefield through server prompts, `SOURCE_UNIT_ENTER_READY` entry metadata names the source object itself, the projected `SOURCE_OBJECT_POWER` effect targets Flameclaw itself with `SourcePath=CoreRuleEngine.ResolveSourceObjectPowerBonus`, `PowerDelta=1`, and `RequiredPlayerExperience=3`, Flameclaw's real attacker battle damage records `basePower=3`, `staticPowerBonus=1`, `combatPower=4`, and `damage=4`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·294/298` Trifarian Training Grounds' battlefield all-units static aura through a legal official Vex deck route: official-opening seed probing selects the battlefield, `UNL-057/219` Wildclaw Beastmaster and an opposing defender are moved there through server prompts, the projected `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` effects target both units from the battlefield source, Wildclaw's real battle damage records `basePower=7`, `staticPowerBonus=1`, `combatPower=8`, and `damage=8`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·297/298` Wind Hill / 疾风山丘's battlefield all-units RULE_TEXT keyword aura through a legal official Vex deck route: official-opening seed probing selects Wind Hill, `UNL-057/219` Wildclaw Beastmaster is played and moved there through server prompts, the projected `BATTLEFIELD_ALL_UNITS_KEYWORD` effect grants `游走` from the battlefield source without adding a printed tag, the server-authored precise battlefield `MOVE_UNIT` pays optional cost `ROAM` to move that unit to a second official P1 battlefield object, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/SameBattlefieldStaticSpellshieldAuraTests.cs` now covers `UNL-041/219` Aerie Head Fan / 艾蕾，头号拥趸 as a same-battlefield other-friendly `RULE_TEXT` keyword aura: continuous-effect projection grants `法盾` only to another friendly unit at the same battlefield, enemy spell targeting that unit pays +1 `spellshieldTaxMana`, the tax disappears when the target moves away, and face-down sources do not project or tax.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `UNL-041/219` Aerie Head Fan / 艾蕾，头号拥趸's same-battlefield other-friendly `法盾` RULE_TEXT aura through a legal official Jhin vs Lillia deck route: Aerie Head Fan and official `OGN·096/298` Watchful Sentinel are played and moved to the same P2 battlefield through server prompts, the projected `SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD` effect grants `法盾` to Watchful Sentinel, official `OGS·003/024` Incinerate targeting that unit pays `baseManaCost=2`, `spellshieldTaxMana=1`, and `totalManaCost=3`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `SFD·159/221` Reliable Siege Dog's source same-location threshold static aura through a legal official Poppy deck route: Reliable Siege Dog and `UNL-092/219` Demacia Envoy are played and moved through server prompts to the same battlefield, the projected `SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER` effect targets Reliable Siege Dog itself with the Envoy as same-location participant, Reliable Siege Dog's real battle damage records `basePower=2`, `staticPowerBonus=1`, `combatPower=3`, and `damage=3`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·240/298` Sett's same-battlefield boon count-to-source static aura through a legal official Poppy deck route: `UNL-092/219` Demacia Envoy is staged to a battlefield, `OGN·136/298` Arena Rookie grants the Envoy `{{增益}}` through a server-authored targeted `PLAY_CARD`, Sett moves to that same battlefield, the projected `SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER` effect targets Sett itself with the boon-bearing Envoy as participant, Sett's real battle damage records `basePower=5`, `staticPowerBonus=1`, `combatPower=6`, and `damage=6`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now carries `OGN·151/298` Lee Sin's same-battlefield other-friendly filtered static aura through a legal official Poppy deck route: `UNL-092/219` Demacia Envoy is staged to a battlefield, `OGN·136/298` Arena Rookie grants the Envoy `{{增益}}` through a server-authored targeted `PLAY_CARD`, Lee Sin moves to the same battlefield, the projected `SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER` effect targets the boon-bearing Envoy and not Lee Sin itself, the Envoy's real battle damage records boon-adjusted `basePower=3`, `staticPowerBonus=2`, `combatPower=5`, and `damage=5`, and the action log replays through score victory to the same final state hash.
- `tests/Riftbound.ConformanceTests/BrushStaticAuraReplacementLifecycleTests.cs` binds Brush's battlefield-filtered static-power aura to the score-time replacement path: matching units keep the Brush `staticPowerBonus` during combat, and the held-score trigger resolves against the replaced battlefield chosen through `BRUSH_USE_REPLACED_BATTLEFIELD:*`.

## Validation Evidence

- Latest same-battlefield other-friendly POWER static-aura scope-router guard focused representative: 1/1 passed.
- Latest same-battlefield other-friendly POWER static-aura scope-router focused behavior representative: 12/12 passed.
- Latest same-battlefield other-friendly POWER static-aura scope-router adjacent StaticAura / StaticPower / SameBattlefield / FullGameEndToEnd / MatchRecovery representative: 2217/2217 passed.
- Latest backend full conformance after same-battlefield other-friendly POWER static-aura scope-router: 9061/9061 passed.
- Latest battlefield RULE_TEXT keyword aura scope-router guard focused representative: 1/1 passed.
- Latest battlefield RULE_TEXT keyword aura scope-router focused behavior representative: 63/63 passed.
- Latest battlefield RULE_TEXT keyword aura scope-router adjacent StaticAura / StaticKeyword / Battlefield / Roam / MoveUnit / FullGameEndToEnd / MatchRecovery representative: 2329/2329 passed.
- Latest backend full conformance after battlefield RULE_TEXT keyword aura scope-router: 9060/9060 passed.
- Latest battlefield POWER static-aura scope-router guard focused representative: 1/1 passed.
- Latest battlefield POWER static-aura scope-router focused behavior representative: 34/34 passed.
- Latest battlefield POWER static-aura scope-router adjacent StaticAura / StaticPower / Battlefield / FullGameEndToEnd / MatchRecovery representative: 2843/2843 passed.
- Latest backend full conformance after battlefield POWER static-aura scope-router: 9059/9059 passed.
- Latest Ornn friendly-equipment runtime bridge removal focused static-aura / Ornn / catalog / equipment keyword representative: 42/42 passed.
- Latest same-location source threshold focused static-aura / catalog representative: 5/5 passed.
- Latest same-location source threshold adjacent StaticAura / StaticPower / ContinuousEffect / ReliableSiegeDog representative: 406/406 passed.
- Latest Master Yi legend static-aura focused representatives: CardCatalogBaseline 252/252, P79LegendStaticMasterYi 3/3, MasterYiLegendStaticAuraSpec 2/2 passed.
- Latest Master Yi legend static-aura adjacent StaticAura / MasterYi / MatchRecovery representative: 2033/2033 passed.
- Latest source combat static-aura projection and recovery scalar focused representatives: SourceCombatStaticAuraProjection + MatchRecovery source-combat powerDelta scalar guard 4/4 passed.
- Latest source combat static-aura projection adjacent SourceCombatStaticAuraProjection / P79ScarletPigeon / P79Waterbender / P79DuneDrake / StaticAura / ContinuousEffect / MatchRecovery representative: 2041/2041 passed.
- Latest source-combat standby participant hidden-boundary focused representatives: 2/2 passed.
- Latest source-combat static-aura projection focused after standby participant guard: 5/5 passed.
- Latest source-combat static-aura / recovery adjacent after standby participant guard: 2044/2044 passed.
- Latest source-combat standby source Core recompute focused representatives: 3/3 passed.
- Latest source-combat static-aura / recovery adjacent after standby source Core guard: 2047/2047 passed.
- Latest static-aura stacking / until-end modifier focused representative: StaticAuraStackingAndModifier 1/1 passed.
- Latest static-aura stacking / source-combat / recovery adjacent representative: 2043/2043 passed.
- Latest source-object filtered static-power battle-damage focused representative: WiseElderSourceObjectFilteredPowerTests 3/3 passed.
- Latest source-object filtered static-power adjacent StaticAura / StaticPower / ContinuousEffect / ReliableSiegeDog / MasterYi representative: 425/425 passed.
- Latest Brush static-aura + replacement lifecycle focused representative: BrushStaticAuraReplacementLifecycleTests 1/1 passed.
- Latest Brush static-aura + replacement lifecycle adjacent BrushReplacement / BattlefieldFilteredStaticPower / BattlefieldHeldScore / StaticAura / ContinuousEffect representative: 415/415 passed.
- Latest battlefield-filtered keyword hidden-boundary focused representative: 2/2 passed.
- Latest battlefield keyword hidden-boundary adjacent BattlefieldFiltered / BattlefieldStatic / StaticAura / StaticKeyword / Roam / Hidden / FaceDown / MatchRecovery representative: 2206/2206 passed.
- Latest battlefield-filtered static-power hidden-boundary focused representative: 2/2 passed.
- Latest battlefield static-power hidden-boundary adjacent BattlefieldFiltered / BattlefieldStatic / StaticAura / StaticPower / Hidden / FaceDown / MatchRecovery representative: 2192/2192 passed.
- Latest battlefield all-units static-power hidden-boundary focused representative: 2/2 passed.
- Latest battlefield all-units static-power hidden-boundary adjacent BattlefieldStatic / BattlefieldAllUnits / StaticAura / StaticPower / Hidden / FaceDown / MatchRecovery representative: 2193/2193 passed.
- Latest other-friendly static-power standby hidden-boundary focused representative: 2/2 passed.
- Latest other-friendly static-power standby hidden-boundary adjacent OtherFriendly / StaticAura / StaticPower / Hidden / Standby / FaceDown / MatchRecovery representative: 2229/2229 passed.
- Latest same-battlefield other-friendly static-power standby hidden-boundary focused representative: 2/2 passed.
- Latest same-battlefield other-friendly static-power standby hidden-boundary adjacent SameBattlefield / StaticAura / StaticPower / Hidden / Standby / FaceDown / MatchRecovery representative: 2233/2233 passed.
- Latest same-battlefield other-friendly static-power card-row focused representative: 5/5 passed.
- Latest same-battlefield other-friendly static-power card-row adjacent CardCatalogBaseline / StaticAura / StaticPower / ContinuousEffect representative: 680/680 passed.
- Latest same-battlefield other-friendly static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative: 21/21 passed.
- Latest Darius same-battlefield other-friendly static-power official-deck full-game focused representative: 1/1 passed.
- Latest same-battlefield other-friendly static-power focused representative after Darius official-deck replay: 7/7 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Darius same-battlefield other-friendly replay: 89/89 passed.
- Latest Darius / SameBattlefieldOtherFriendly / SameBattlefieldStaticAura / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Darius same-battlefield other-friendly replay: 2166/2166 passed.
- Latest Ornn friendly-equipment count-to-source static-power official-deck full-game focused representative: 1/1 passed.
- Latest Ornn friendly-equipment static-aura / LayerEngine focused representative after official-deck replay: 64/64 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Ornn friendly-equipment replay: 90/90 passed.
- Latest Ornn / FriendlyEquipment / EquipmentKeyword / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Ornn friendly-equipment replay: 2200/2200 passed.
- Latest other-friendly static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Baron Nashor other-friendly replay: 25/25 passed.
- Latest OtherFriendly / StaticAura / StaticPower / Baron / FullGameEndToEnd / MatchRecovery adjacent representative after Baron Nashor other-friendly replay: 2114/2114 passed.
- Latest source-combat static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Scarlet Pigeon source-combat replay: 26/26 passed.
- Latest SourceCombat / StaticAura / StaticPower / Scarlet / FullGameEndToEnd / MatchRecovery adjacent representative after Scarlet Pigeon source-combat replay: 2106/2106 passed.
- Latest source-attacking-ready-enemy static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Dune Drake source-attacking-ready-enemy replay: 35/35 passed.
- Latest DuneDrake / SourceAttackingReadyEnemy / SourceCombat / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Dune Drake source-attacking-ready-enemy replay: 2114/2114 passed.
- Latest same-battlefield ephemeral count-to-source static-power official-deck midgame focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Petal Pixie same-battlefield ephemeral count-to-source replay: 36/36 passed.
- Latest PetalPixie / SameBattlefieldFriendlyFiltered / Ephemeral / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Petal Pixie same-battlefield ephemeral count-to-source replay: 2139/2139 passed.
- Latest friendly-token static-power official-deck midgame focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Soul Shepherd friendly-token replay: 37/37 passed.
- Latest SoulShepherd / FriendlyFiltered / UnitToken / Token / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Soul Shepherd friendly-token replay: 2203/2203 passed.
- Latest friendly-mechanical static-power official-deck midgame focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Rumble friendly-mechanical replay: 38/38 passed.
- Latest Rumble / FriendlyFiltered / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Rumble friendly-mechanical replay: 2120/2120 passed.
- Latest battlefield isolated-defender keyword-modifier focused/projection official-deck representative: 3/3 passed.
- Latest FullGameEndToEnd B0/B2 cross-slice representative after Forbidden Wasteland battlefield isolated-defender replay: 39/39 passed.
- Latest BattlefieldIsolatedDefenderKeywordModifier / BattlefieldIsolated / ForbiddenWasteland / StaticAura / StaticKeyword / Steadfast / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Forbidden Wasteland battlefield isolated-defender replay: 2111/2111 passed.
- Latest friendly-filtered Steadfast RULE_TEXT official-deck midgame focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B2 cross-slice representative after Rumble legend friendly-mechanical Steadfast replay: 40/40 passed.
- Latest RumbleLegendFriendlyMechanicalSteadfast / FriendlyFiltered / StaticKeyword / StaticAura / Steadfast / Rumble / FullGameEndToEnd / MatchRecovery adjacent representative after Rumble legend friendly-mechanical Steadfast replay: 2112/2112 passed.
- Latest battlefield all-units static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Trifarian Training Grounds battlefield all-units replay: 27/27 passed.
- Latest BattlefieldAllUnits / BattlefieldStatic / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Trifarian Training Grounds battlefield all-units replay: 2141/2141 passed.
- Latest battlefield all-units RULE_TEXT Roam official-deck full-game focused representative: 1/1 passed.
- Latest BattlefieldAllUnits / BattlefieldStatic / StaticKeyword / StaticAura / Roam / FullGameEndToEnd / MatchRecovery adjacent representative after Wind Hill battlefield all-units keyword replay: 2212/2212 passed.
- Latest backend full after battlefield all-units RULE_TEXT Roam official-deck full-game evidence: 8855/8855 passed.
- Latest source same-location static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Reliable Siege Dog source same-location replay: 28/28 passed.
- Latest SourceSameLocation / ReliableSiegeDog / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Reliable Siege Dog source same-location replay: 2101/2101 passed.
- Latest same-battlefield boon count-to-source static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Sett same-battlefield boon count-to-source replay: 29/29 passed.
- Latest Sett / Boon / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Sett same-battlefield boon count-to-source replay: 2214/2214 passed.
- Latest same-battlefield other-friendly filtered static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Lee Sin same-battlefield other-friendly filtered replay: 30/30 passed.
- Latest LeeSin / Boon / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representative after Lee Sin same-battlefield other-friendly filtered replay: 2206/2206 passed.
- Latest source-lone-battle static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Waterbender source-lone-battle replay: 31/31 passed.
- Latest Waterbender / SourceLoneBattle / SourceCombat / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / WatchfulSentinel / MatchRecovery adjacent representative after Waterbender source-lone-battle replay: 2116/2116 passed.
- Latest friendly single-defender static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Master Yi intro friendly single-defender replay: 32/32 passed.
- Latest MasterYi / FriendlySingleDefender / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / DemaciaEnvoy / MatchRecovery adjacent representative after Master Yi intro friendly single-defender replay: 2108/2108 passed.
- Latest friendly-units experience-gated static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Master Yi level friendly-units replay: 33/33 passed.
- Latest MasterYi / FriendlyUnitsPower / Experience / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / DemaciaEnvoy / MatchRecovery adjacent representative after Master Yi level friendly-units replay: 2163/2163 passed.
- Latest source-object filtered static-power official-deck full-game focused representative: 1/1 passed.
- Latest FullGameEndToEnd B0/B1 cross-slice representative after Wise Elder source-object filtered replay: 34/34 passed.
- Latest WiseElder / SourceObjectFiltered / Boon / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery / ArenaRookie adjacent representative after Wise Elder source-object filtered replay: 2207/2207 passed.
- Latest friendly-equipment static-power standby hidden-boundary focused representative: 1/1 passed.
- Latest friendly-equipment static-power standby hidden-boundary adjacent Ornn / EquipmentKeyword / StaticAura / StaticPower / Standby / Hidden / FaceDown / MatchRecovery representative: 2257/2257 passed.
- Latest Ornn friendly-equipment runtime bridge removal adjacent StaticAura / Ornn / EquipmentKeyword / LayerEngine / ContinuousEffect representative: 417/417 passed.
- Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT focused representative: 5/5 passed.
- Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT adjacent / hidden-info representative: 2100/2100 passed.
- Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT official-deck replay focused representative: 1/1 passed.
- Latest Aerie Head Fan same-battlefield Spellshield RULE_TEXT official-deck replay adjacent / hidden-info representative: 2141/2141 passed.
- Latest backend full after Aerie Head Fan same-battlefield Spellshield RULE_TEXT official-deck replay evidence: 8941/8941 passed.
- Latest MatchRecovery hidden-information boundary: 1989/1989 passed.
- Latest MatchRecovery hidden-information boundary after source-object filtered static-power evidence: 1989/1989 passed.
- Latest MatchRecovery hidden-information boundary after same-battlefield other-friendly card-row evidence: 1989/1989 passed.
- Latest MatchRecovery hidden-information boundary after Brush static-aura + replacement lifecycle evidence: 1989/1989 passed.
- Latest backend full: 8602/8602 passed.
- Latest backend full after battlefield keyword hidden-boundary fix: 8619/8619 passed.
- Latest backend full after battlefield static-power hidden-boundary fix: 8621/8621 passed.
- Latest backend full after battlefield all-units static-power hidden-boundary fix: 8623/8623 passed.
- Latest backend full after other-friendly static-power standby hidden-boundary fix: 8625/8625 passed.
- Latest backend full after same-battlefield other-friendly static-power standby hidden-boundary fix: 8627/8627 passed.
- Latest backend full after friendly-equipment static-power standby hidden-boundary fix: 8628/8628 passed.
- Latest backend full after source-combat standby participant hidden-boundary fix: 8631/8631 passed.
- Latest backend full after source-combat standby source Core guard: 8634/8634 passed.
- Latest backend full after source-object filtered static-power evidence: 8705/8705 passed.
- Latest backend full after same-battlefield other-friendly static-power card-row evidence: 8710/8710 passed.
- Latest backend full after Brush static-aura + replacement lifecycle evidence: 8711/8711 passed.
- Latest backend full after same-battlefield other-friendly static-power official-deck full-game evidence: 8713/8713 passed.
- Latest backend full after other-friendly static-power official-deck full-game evidence: 8717/8717 passed.
- Latest backend full after source-combat static-power official-deck full-game evidence: 8718/8718 passed.
- Latest backend full after battlefield all-units static-power official-deck full-game evidence: 8719/8719 passed.
- Latest backend full after source same-location static-power official-deck full-game evidence: 8720/8720 passed.
- Latest backend full after same-battlefield boon count-to-source static-power official-deck full-game evidence: 8721/8721 passed.
- Latest backend full after same-battlefield other-friendly filtered static-power official-deck full-game evidence: 8722/8722 passed.
- Latest backend full after source-lone-battle static-power official-deck full-game evidence: 8723/8723 passed.
- Latest backend full after friendly single-defender static-power official-deck full-game evidence: 8724/8724 passed.
- Latest backend full after friendly-units experience-gated static-power official-deck full-game evidence: 8725/8725 passed.
- Latest backend full after source-object filtered static-power official-deck full-game evidence: 8726/8726 passed.
- Latest backend full after Dune Drake source-attacking-ready-enemy static-power official-deck full-game evidence: 8727/8727 passed.
- Latest backend full after Petal Pixie same-battlefield ephemeral count-to-source static-power official-deck midgame evidence: 8728/8728 passed.
- Latest backend full after Soul Shepherd friendly-token static-power official-deck midgame evidence: 8729/8729 passed.
- Latest backend full after Rumble friendly-mechanical static-power official-deck midgame evidence: 8730/8730 passed.
- Latest backend full after Forbidden Wasteland battlefield isolated-defender keyword-modifier evidence: 8733/8733 passed.
- Latest backend full after Rumble legend friendly-mechanical Steadfast official-deck midgame evidence: 8734/8734 passed.
- Latest backend full after Darius same-battlefield other-friendly static-power official-deck full-game evidence: 8853/8853 passed.
- Latest backend full after Ornn friendly-equipment count-to-source static-power official-deck full-game evidence: 8854/8854 passed.
- Latest backend full after Aerie Head Fan same-battlefield Spellshield RULE_TEXT evidence: 8929/8929 passed.
- Latest source-object level static-power focused representative: 5/5 passed.
- Latest SourceObjectLevelPower / SourceObjectPower / SourceObjectFiltered / StaticAura / StaticPower / ContinuousEffect / Experience / MatchRecovery adjacent representative: 2149/2149 passed.
- Latest backend full after source-object level static-power evidence: 8954/8954 passed.
- Latest Crystalhand Hunter source-object level static-power official-deck focused representative: 1/1 passed.
- Latest Crystalhand Hunter / SourceObjectLevelPower / SourceObjectPower / SourceObjectFiltered / StaticAura / StaticPower / ContinuousEffect / Experience / FullGameEndToEnd / MatchRecovery adjacent representative: 2243/2243 passed.
- Latest backend full after Crystalhand Hunter source-object level static-power official-deck evidence: 8955/8955 passed.
- Latest Targon Seer source-object level static-power official-deck focused representative: 1/1 passed.
- Latest Targon Seer / SourceObjectLevelPower / SourceObjectPower / SourceObjectFiltered / StaticAura / StaticPower / ContinuousEffect / Experience / FullGameEndToEnd / MatchRecovery adjacent representative: 2244/2244 passed.
- Latest backend full after Targon Seer source-object level static-power official-deck evidence: 8956/8956 passed.
- Latest Flameclaw level-gated active-entry + source-object level static-power official-deck focused representative: 4/4 passed.
- Latest Flameclaw / SourceUnitLevelActiveEntry / SourceUnitEnterReady / ActiveEntry / StaticAbility / StaticAura / SourceObjectLevelPower / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent representative: 2501/2501 passed.
- Latest backend full after Flameclaw level-gated active-entry + source-object level static-power official-deck evidence: 8971/8971 passed.
- Latest DevUi build after catalog type sync: passed.

## Remaining Evidence Needed

Before B1 can be called complete, later slices still need evidence for:

- Broader Brush replacement / actual swap-back lifecycle beyond the current score-time replacement representative.
- Other source-unit count-to-source and threshold static auras beyond the Ornn friendly-equipment count-to-source official-deck representative, the Petal Pixie same-battlefield ephemeral count-to-source midgame representative, the Reliable Siege Dog source same-location official-deck representative, the Sett same-battlefield boon count-to-source official-deck representative, and the Flameclaw / Crystalhand Hunter / Targon Seer source-object level-power official-deck representatives, plus source combat static-aura official-deck breadth beyond the current Scarlet Pigeon, Dune Drake, and Waterbender representatives.
- Additional full-game official-deck routes for static-aura / RULE_TEXT modifier families beyond the current Garen and Darius same-battlefield other-friendly, Aerie Head Fan same-battlefield Spellshield, Ornn friendly-equipment count-to-source, Baron Nashor non-local other-friendly, Scarlet Pigeon source-combat, Dune Drake source-attacking-ready-enemy, Petal Pixie same-battlefield ephemeral count-to-source, Soul Shepherd friendly-token, Rumble friendly-mechanical filtered self-boost, Rumble legend friendly-mechanical Steadfast, Forbidden Wasteland battlefield isolated-defender keyword modifier, Waterbender source-lone-battle, Master Yi intro friendly single-defender, Master Yi level friendly-units, Wise Elder source-object filtered, Flameclaw / Crystalhand Hunter / Targon Seer source-object level-power, Trifarian Training Grounds battlefield all-units, Reliable Siege Dog source same-location, Sett same-battlefield boon count-to-source, and Lee Sin same-battlefield other-friendly filtered representatives.
- Broader multiple static auras and aura stacking beyond the current source-combat + other-friendly additive representative.
- Broader interaction with until-end-of-turn power modifiers beyond the current representative coverage.
- Additional conditional subscopes, keyword removal, and remaining RULE_TEXT keyword grant / modifier scopes.
- Full official static-aura breadth and `git diff --check` after each final slice.
