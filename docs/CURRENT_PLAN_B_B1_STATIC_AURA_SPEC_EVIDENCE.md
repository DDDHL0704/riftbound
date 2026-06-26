# Plan B B1 Static Aura Spec Evidence

更新时间：2026-06-26

## Evidence Summary

This evidence records the current B1 static-aura data-driven slices.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` defines `StaticAuraSpec`, `StaticAuraKinds`, `StaticAuraTargetScopes`, and `StaticAuraParticipantScopes`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses the current representative static-aura text patterns, including battlefield all-units power, battlefield all-units keyword, battlefield-filtered, same-battlefield friendly-filtered count-to-source, same-battlefield boon count-to-source, same-location other-friendly source threshold, friendly single-defender combat power, experience-gated friendly-unit power, source-object combat power, same-battlefield other-friendly, same-battlefield friendly-filtered, non-local other-friendly, and friendly-filtered unit power / keyword auras.
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` exposes parsed static auras through `BehaviorSpec.StaticAuras`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Ornn, Tifarian Training Grounds, Brush, Petal Pixie, Sett, Reliable Siege Dog, Master Yi intro, Master Yi level, Scarlet Pigeon, Waterbender, Dune Drake, Soul Shepherd, Rumble, Lee Sin, Blackflame Altar, Forbidden Wasteland, and Wind Hill representative static-aura specs, plus false-positive guards for Boon-granting text and Brush reminder text on Ivern's legend.
- `StaticAuraSpec.RequiredParticipantCount` records threshold-style conditions where at least N participants enable a fixed `PowerDeltaPerParticipant` rather than multiplying the power delta by all participants.
- `StaticAuraSpec.RequiredPlayerExperience` records level/experience thresholds for static auras such as Master Yi level's `{{等级6>}}` friendly-unit power.

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
- `src/Riftbound.Engine/CoreRuleEngine.cs` now resolves Ornn-style friendly-equipment count-to-source power recompute and source-unit entry power through `StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura` and `StaticAuraSpec.PowerDeltaPerParticipant`; `CardBehaviorDefinition.AddsFriendlyFieldEquipmentCountToSourceUnitPower` has been deleted.
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` now marks the friendly-equipment static-power representative boundary from `BehaviorSpec.StaticAuras` rather than a registry runtime flag.
- `src/Riftbound.Engine/MatchSession.cs` now excludes standby sources from Ornn-style friendly-equipment static-power projection, and `src/Riftbound.Engine/CoreRuleEngine.cs` excludes standby sources before friendly-equipment source-power recompute.
- `src/Riftbound.Engine/CoreRuleEngine.cs` and `src/Riftbound.Engine/MatchSession.cs` grant battlefield static `ROAM` from `StaticAuraSpec.Kind=BATTLEFIELD_ALL_UNITS_KEYWORD` + `GrantedKeyword=游走`; the old `BattlefieldStaticRoamCardNo` / `IsBattlefieldStaticRoamCardNo` branches are removed.
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
- `tests/Riftbound.ConformanceTests/StaticAuraStackingAndModifierTests.cs` verifies that Scarlet Pigeon's source-combat static aura, Baron Nashor's other-friendly static aura, and an until-end-of-turn power modifier stack additively in continuous-effect projection and real `DECLARE_BATTLE` combat damage.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the optional `requiredParticipantCount` and `requiredPlayerExperience` protocol fields for static-aura catalog payloads.

Recovery:

- `src/Riftbound.Engine/MatchRecovery.cs` validates object and battlefield static-aura source cards against the `BehaviorSpec` aura surface, including legend-source friendly-unit power auras.
- `src/Riftbound.Engine/MatchRecovery.cs` validates same-location source threshold object static auras with fixed power delta even when multiple participant object ids satisfy the threshold.
- `src/Riftbound.Engine/MatchRecovery.cs` validates source-object combat static-aura durations, effect ids, source paths, conditions, lifecycle strings, source cards, and fixed `powerDelta` scalars through `StaticAuraSpecRules`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` updates the source-card drift expectation to the spec-driven diagnostic.
- `tests/Riftbound.ConformanceTests/SameBattlefieldOtherFriendlyStaticPowerCardRowTests.cs` binds all current official `此处的其他友方单位获得{{S}}+1` rows to the same spec-driven projection and combat-damage path, covering `OGS·013/024`, `SFD·236/221`, `SFD·236*/221`, `OGN·243/298`, and `OGN·243a/298`.

## Validation Evidence

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
- Latest friendly-equipment static-power standby hidden-boundary focused representative: 1/1 passed.
- Latest friendly-equipment static-power standby hidden-boundary adjacent Ornn / EquipmentKeyword / StaticAura / StaticPower / Standby / Hidden / FaceDown / MatchRecovery representative: 2257/2257 passed.
- Latest Ornn friendly-equipment runtime bridge removal adjacent StaticAura / Ornn / EquipmentKeyword / LayerEngine / ContinuousEffect representative: 417/417 passed.
- Latest MatchRecovery hidden-information boundary: 1989/1989 passed.
- Latest MatchRecovery hidden-information boundary after source-object filtered static-power evidence: 1989/1989 passed.
- Latest MatchRecovery hidden-information boundary after same-battlefield other-friendly card-row evidence: 1989/1989 passed.
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
- Latest DevUi build after catalog type sync: passed.

## Remaining Evidence Needed

Before B1 can be called complete, later slices still need evidence for:

- Brush replacement / score-time swap-back lifecycle beyond the token's static aura.
- Other source-unit count-to-source and threshold static auras beyond Petal Pixie / Sett / Reliable Siege Dog, plus source combat static-aura breadth beyond the current Scarlet Pigeon / Waterbender / Dune Drake representatives.
- Broader multiple static auras and aura stacking beyond the current source-combat + other-friendly additive representative.
- Broader interaction with until-end-of-turn power modifiers beyond the current representative coverage.
- Additional conditional subscopes, keyword removal, and remaining RULE_TEXT keyword grant scopes.
- Full official static-aura breadth and `git diff --check` after each final slice.
