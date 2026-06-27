# Plan B Other-Friendly Active-Entry Static Ability Spec Evidence

更新时间：2026-06-27

## Evidence Summary

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilityKinds.OtherFriendlyUnitsEnterReady`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `当我在场上时，其他友方单位以活跃状态进场。` into a `StaticAbilitySpec`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies `OGN·011/298` 熔浆巨龙 exposes `OTHER_FRIENDLY_UNITS_ENTER_READY` through `BehaviorSpec.StaticAbilities`.

Runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetOtherFriendlyUnitsEnterReadyAbility`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` enumerates public field objects and applies the static ability only when the source is a face-up, non-standby friendly unit controlled by the entering unit's controller.
- The entering object id is excluded, so a source does not make itself enter ready through its own "other friendly" static text.
- The active-entry check is shared by `PlaySourceUnitToBase` and `PlaySourceUnitToBattlefield`, so future cards with this parsed shape do not require engine card-number branches.
- `tests/Riftbound.ConformanceTests/MoltenDrakeOtherFriendlyActiveEntryTests.cs` proves an unpaid-haste `OGN·010/298` 军团后卫 that would normally enter exhausted instead enters ready while friendly public 熔浆巨龙 is on base.
- The same test file proves a face-down / standby 熔浆巨龙 source does not grant active entry and does not emit static-entry metadata.

## Validation Evidence

- Focused pre-implementation red: `BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility` had no matching static ability and `MoltenDrakeMakesOtherFriendlyUnpaidHasteUnitEnterReadyFromStaticAbilitySpec` observed the target still exhausted.
- Focused post-implementation: 3/3 passed.
- Adjacent CardCatalogBaseline / HasteReady / MatchRecovery regression: 2306/2306 passed.
- Backend full: 8841/8841 passed.

## Remaining Evidence Needed

- Official-deck score-victory replay coverage for 熔浆巨龙 remains open.
- Broader active-entry static ability families remain open: low-hand active entry, level-gated active entry, token-only active entry, turn-scoped active-entry effects, and source-zone / battlefield-entry variants.
- Project remains NOT READY.
