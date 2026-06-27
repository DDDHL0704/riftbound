# Plan B Active-Entry Static Ability Spec Evidence

更新时间：2026-06-27

## Evidence Summary

Other-friendly unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilityKinds.OtherFriendlyUnitsEnterReady`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `当我在场上时，其他友方单位以活跃状态进场。` into a `StaticAbilitySpec`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies `OGN·011/298` 熔浆巨龙 exposes `OTHER_FRIENDLY_UNITS_ENTER_READY` through `BehaviorSpec.StaticAbilities`.

Other-friendly unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetOtherFriendlyUnitsEnterReadyAbility`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` enumerates public field objects and applies the static ability only when the source is a face-up, non-standby friendly unit controlled by the entering unit's controller.
- The entering object id is excluded, so a source does not make itself enter ready through its own "other friendly" static text.
- The active-entry check is shared by `PlaySourceUnitToBase` and `PlaySourceUnitToBattlefield`, so future cards with this parsed shape do not require engine card-number branches.
- `tests/Riftbound.ConformanceTests/MoltenDrakeOtherFriendlyActiveEntryTests.cs` proves an unpaid-haste `OGN·010/298` 军团后卫 that would normally enter exhausted instead enters ready while friendly public 熔浆巨龙 is on base.
- The same test file proves a face-down / standby 熔浆巨龙 source does not grant active entry and does not emit static-entry metadata.

Filtered token active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilityKinds.FriendlyFilteredUnitsEnterReady`, `StaticAbilitySpec.TargetFilter`, and `StaticAuraTargetFilters.Token`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `你的指示物以活跃状态进场。` into `FRIENDLY_FILTERED_UNITS_ENTER_READY` with `TargetFilter=TOKEN`.
- `tests/Riftbound.ConformanceTests/RenataTokenActiveEntryStaticAbilityTests.cs` verifies both `SFD·171/221` and `SFD·171a/221` expose that spec through `BehaviorSpec.StaticAbilities`.

Filtered token active-entry runtime:

- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs` exposes `IsTokenFactory`, covering unit / equipment / battlefield token factory identities without local card-number checks.
- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetFriendlyFilteredUnitsEnterReadyAbility`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` routes both `OTHER_FRIENDLY_UNITS_ENTER_READY` and `FRIENDLY_FILTERED_UNITS_ENTER_READY` through `TryGetFriendlyUnitEnterReadyStaticAbilitySource`.
- `ApplyUnitTokenEntryStaticAbility` applies the same entry source scan to token factory unit entries and emits shared `entryStaticAbility*` audit metadata.
- `tests/Riftbound.ConformanceTests/RenataTokenActiveEntryStaticAbilityTests.cs` proves public face-up Renata marks an Azir-created `SFD·T02` 黄沙士兵 token entry with `FRIENDLY_FILTERED_UNITS_ENTER_READY`, while face-down / standby Renata does not.
- Adjacent token fixtures prove existing Azir, Viktor, battlefield held, Mechanical Trickster, Ironclad Vanguard, and Warhawk token routes still preserve token creation behavior.

## Validation Evidence

- Focused pre-implementation red: `BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility` had no matching static ability and `MoltenDrakeMakesOtherFriendlyUnpaidHasteUnitEnterReadyFromStaticAbilitySpec` observed the target still exhausted.
- Focused post-implementation: 3/3 passed.
- Adjacent CardCatalogBaseline / HasteReady / MatchRecovery regression: 2306/2306 passed.
- Renata filtered token pre-implementation red: `RenataTokenActiveEntryStaticAbilityTests` failed at compile because `FRIENDLY_FILTERED_UNITS_ENTER_READY` / `TargetFilter` did not exist.
- Renata filtered token focused post-implementation: 3/3 passed.
- Renata / Molten / token factory adjacent regression: 335/335 passed.
- Backend full after this slice: 8844/8844 passed.

## Remaining Evidence Needed

- Official-deck score-victory replay coverage for 熔浆巨龙 remains open.
- Broader active-entry static ability families remain open: low-hand active entry, level-gated active entry, equipment / battlefield token entry payload coverage, turn-scoped active-entry effects, and source-zone / battlefield-entry variants.
- Project remains NOT READY.
