# Plan B Active-Entry Static Ability Spec Evidence

更新时间：2026-06-29

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
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now also proves a legal official Jhin deck opening can feed Molten Drake other-friendly active-entry into B0 score victory: P1 keeps public face-up `OGN·011/298` 熔浆巨龙 in base, plays official `OGN·010/298` 军团后卫 to a P1 battlefield without paying `HASTE_READY`, receives `OTHER_FRIENDLY_UNITS_ENTER_READY` entry metadata from Molten Drake, and replays the same action log to final score victory without hidden-zone leaks.

Filtered token active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilityKinds.FriendlyFilteredUnitsEnterReady`, `StaticAbilitySpec.TargetFilter`, and `StaticAuraTargetFilters.Token`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `你的指示物以活跃状态进场。` into `FRIENDLY_FILTERED_UNITS_ENTER_READY` with `TargetFilter=TOKEN`.
- `tests/Riftbound.ConformanceTests/RenataTokenActiveEntryStaticAbilityTests.cs` verifies both `SFD·171/221` and `SFD·171a/221` expose that spec through `BehaviorSpec.StaticAbilities`.

Filtered token active-entry runtime:

- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs` exposes `IsTokenFactory`, covering unit / equipment / battlefield token factory identities without local card-number checks.
- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetFriendlyFilteredUnitsEnterReadyAbility`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` routes `OTHER_FRIENDLY_UNITS_ENTER_READY` and `FRIENDLY_FILTERED_UNITS_ENTER_READY` through the same public-source scan, while keeping `OTHER_FRIENDLY_UNITS_ENTER_READY` unit-only.
- `ApplyTokenEntryStaticAbility` applies the entry source scan to token factory unit and equipment entries and emits shared `entryStaticAbility*` audit metadata.
- `tests/Riftbound.ConformanceTests/RenataTokenActiveEntryStaticAbilityTests.cs` proves public face-up Renata marks an Azir-created `SFD·T02` 黄沙士兵 token entry with `FRIENDLY_FILTERED_UNITS_ENTER_READY`, while face-down / standby Renata does not.
- `tests/Riftbound.ConformanceTests/RenataTokenActiveEntryStaticAbilityTests.cs` now also proves public face-up Renata makes a Pyke-created `UNL·T05` Gold equipment token enter ready, while face-down / standby Renata leaves that Gold token exhausted and emits no entry-static metadata.
- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves equipment token factory identity by token family plus source set, so UNL sources create `UNL·T05` Gold and SFD sources create `SFD·T03` Gold without local source-card branches.
- Gold equipment token fixtures now expect official token identity payloads / states: `tokenCardNo=SFD·T03`, tags `[CARD_TYPE:EQUIPMENT, 反应, 金币]`, owner/controller, and exhausted state unless a valid entry-static ability changes it.
- Adjacent token fixtures prove existing Azir, Viktor, battlefield held, Mechanical Trickster, Ironclad Vanguard, and Warhawk token routes still preserve token creation behavior.

Level-gated friendly unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilityKinds.FriendlyUnitsEnterReady` and `StaticAbilitySpec.RequiredPlayerExperience`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `{{等级11>}} 你的单位以活跃状态进场。` into `FRIENDLY_UNITS_ENTER_READY` with `RequiredPlayerExperience=11`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `requiredPlayerExperience` on `staticAbilities` for the shared catalog payload shape.
- `tests/Riftbound.ConformanceTests/MasterYiLevelActiveEntryStaticAbilityTests.cs` verifies `UNL-191/219`, `UNL-231/219`, and `UNL-231*/219` expose that spec through `BehaviorSpec.StaticAbilities`.

Level-gated friendly unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetFriendlyUnitsEnterReadyAbility`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` routes played unit entry through `StaticAbilitySpec.Kind=FRIENDLY_UNITS_ENTER_READY` and checks `RequiredPlayerExperience` against the entering unit controller.
- `CoreRuleEngine` now scans controlled public legend-zone sources for this generic friendly-unit active-entry static ability, so Master Yi level 11 no longer needs `ControllerHasMasterYiLevelLegend`, `MasterYiLevelReadyThreshold`, or an `entersActiveFromMasterYiLevel` branch.
- `tests/Riftbound.ConformanceTests/MasterYiLevelActiveEntryStaticAbilityTests.cs` proves Master Yi level 11 makes an unpaid-haste `OGN·010/298` 军团后卫 enter ready at 11 experience and emits `entryStaticAbilityKind=FRIENDLY_UNITS_ENTER_READY` with legend source object/card metadata.
- The same test proves 10 experience does not satisfy the parsed requirement and leaves the unpaid-haste unit exhausted with no entry-static metadata.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now also proves a legal official Master Yi level deck opening can feed level 11 `FRIENDLY_UNITS_ENTER_READY` into B0 score victory: P1 has `UNL-191/219` in the legend zone and 11 experience, plays official `UNL-092/219` 德玛西亚使节 to a P1 battlefield without `HASTE_READY`, receives legend-source entry metadata from Master Yi, and replays the same action log to final score victory without hidden-zone leaks.

Low-hand source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilityKinds.SourceUnitEnterReady` and `StaticAbilitySpec.MaxControllerHandCount`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `如果你的手牌不超过两张，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `MaxControllerHandCount=2`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `maxControllerHandCount` on `staticAbilities` for the shared catalog payload shape.
- `tests/Riftbound.ConformanceTests/DunehornLowHandActiveEntryStaticAbilityTests.cs` verifies `SFD·027/221` exposes that spec through `BehaviorSpec.StaticAbilities`.

Low-hand source-unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetSourceUnitEnterReadyAbility`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through `StaticAbilitySpec.Kind=SOURCE_UNIT_ENTER_READY` and checks `MaxControllerHandCount` against the controller's hand count after the played card has left hand.
- `tests/Riftbound.ConformanceTests/DunehornLowHandActiveEntryStaticAbilityTests.cs` proves Dunehorn Beast enters ready when the controller has two cards in hand after play and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves three cards in hand after play does not satisfy the parsed requirement and leaves the pre-exhausted source unit exhausted with no entry-static metadata.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now also proves a legal official Jhin deck opening can feed Dunehorn Beast low-hand active-entry into B0 score victory: P1 plays `SFD·027/221` from a three-card hand, the post-play two-card hand satisfies `SOURCE_UNIT_ENTER_READY`, `UNIT_PLAYED_TO_BATTLEFIELD` emits self source metadata, and the same action log replays to the final score-victory state hash without hidden-zone leaks.

Level-gated source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `{{等级3>}} 我获得{{S}}+1，并以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredPlayerExperience=3`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` also parses `{{等级3>}} 我以活跃状态进场。` into the same `SOURCE_UNIT_ENTER_READY` shape with `RequiredPlayerExperience=3`.
- `tests/Riftbound.ConformanceTests/SourceUnitLevelActiveEntryStaticAbilityTests.cs` verifies `UNL-016/219` 焰爪 and `UNL-151/219` 班德尔士兵 expose this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Level-gated source-unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetSourceUnitEnterReadyAbility` for source-unit active-entry specs that carry either `MaxControllerHandCount` or `RequiredPlayerExperience`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through `StaticAbilitySpec.Kind=SOURCE_UNIT_ENTER_READY` and checks `RequiredPlayerExperience` against the entering unit controller before deciding whether the source unit enters ready.
- `tests/Riftbound.ConformanceTests/SourceUnitLevelActiveEntryStaticAbilityTests.cs` proves Flameclaw enters ready at 3 experience and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves 2 experience does not satisfy the parsed requirement and leaves Flameclaw exhausted with no entry-static metadata.
- `tests/Riftbound.ConformanceTests/SourceUnitLevelActiveEntryStaticAbilityTests.cs` also proves Bandle Soldier enters ready at 3 experience, stays exhausted below level 3, and emits the same self-source entry metadata when the requirement is satisfied.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now proves a legal official Jhin deck opening can feed Flameclaw level-gated active-entry and source-object static-power into B0 score victory: P1 has 3 experience, plays `UNL-016/219` directly to a battlefield, receives `SOURCE_UNIT_ENTER_READY` self metadata, projects `SOURCE_OBJECT_POWER` with `PowerDelta=1`, deals 4 real combat damage from base 3 plus static 1, and replays the same action log to the final score-victory state hash without hidden-zone leaks.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now also proves a legal official Poppy deck opening can feed `UNL-151/219` Bandle Soldier level-gated active-entry into B0 score victory: P1 has 3 experience, plays Bandle Soldier directly to a battlefield, receives `SOURCE_UNIT_ENTER_READY` self metadata, enters active with printed 5 power and no continuous-effect projection, and replays the same action log to the final score-victory state hash without hidden-zone leaks.

## Validation Evidence

- Focused pre-implementation red: `BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility` had no matching static ability and `MoltenDrakeMakesOtherFriendlyUnpaidHasteUnitEnterReadyFromStaticAbilitySpec` observed the target still exhausted.
- Focused post-implementation: 3/3 passed.
- Adjacent CardCatalogBaseline / HasteReady / MatchRecovery regression: 2306/2306 passed.
- Molten Drake other-friendly active-entry B0 official-deck replay focused: 1/1 passed.
- Molten Drake active-entry replay / active-entry / FullGameEndToEnd / MatchRecovery adjacent regression: 2103/2103 passed.
- Renata filtered token pre-implementation red: `RenataTokenActiveEntryStaticAbilityTests` failed at compile because `FRIENDLY_FILTERED_UNITS_ENTER_READY` / `TargetFilter` did not exist.
- Renata filtered token focused post-implementation after unit-token support: 3/3 passed.
- Renata / Molten / token factory adjacent regression: 335/335 passed.
- Renata equipment-token pre-implementation red: `RenataMakesFriendlyEquipmentTokenEnterReadyFromStaticAbilitySpec` observed the Gold token still exhausted, and the face-down-source guard observed no `tokenCardNo` payload.
- Renata equipment-token focused post-implementation: 5/5 passed.
- Gold / trigger adjacent regression: 190/190 passed.
- Conformance fixture runner after Gold token identity sync: 3108/3108 passed.
- Master Yi level-gated active-entry pre-implementation red: `MasterYiLevelActiveEntryStaticAbilityTests` failed at compile because `FRIENDLY_UNITS_ENTER_READY` / `RequiredPlayerExperience` did not exist.
- Master Yi level-gated active-entry focused post-implementation: 4/4 passed.
- Master Yi / Molten / Renata / CardCatalogBaseline adjacent regression: 307/307 passed.
- MatchRecovery hidden-info / continuous-effect guard regression: 1984/1984 passed.
- Master Yi level-gated active-entry B0 official-deck replay focused: 1/1 passed.
- Master Yi level active-entry replay / active-entry / FullGameEndToEnd / MatchRecovery adjacent regression: 2104/2104 passed.
- Dunehorn low-hand active-entry pre-implementation red: `DunehornLowHandActiveEntryStaticAbilityTests` failed at compile because `SOURCE_UNIT_ENTER_READY` / `MaxControllerHandCount` did not exist.
- Dunehorn low-hand active-entry focused post-implementation: 3/3 passed.
- Dunehorn / active-entry / MatchRecovery adjacent regression: 2297/2297 passed.
- Dunehorn low-hand active-entry B0 official-deck replay focused: 1/1 passed.
- Dunehorn low-hand active-entry replay / active-entry / FullGameEndToEnd / MatchRecovery adjacent regression: 2100/2100 passed.
- Flameclaw level-gated source-unit active-entry pre-implementation red: `SourceUnitLevelActiveEntryStaticAbilityTests` had no matching parsed `SOURCE_UNIT_ENTER_READY` spec and observed Flameclaw still exhausted at 3 experience.
- Flameclaw level-gated source-unit active-entry focused post-implementation: 3/3 passed.
- Flameclaw level-gated active-entry + source-object static-power B0 official-deck replay focused: 4/4 passed.
- Flameclaw active-entry / static-aura / FullGameEndToEnd / MatchRecovery adjacent regression: 2501/2501 passed.
- Bandle Soldier level-gated source-unit active-entry focused: 6/6 passed.
- Bandle Soldier level-gated source-unit active-entry + B0 official-deck replay focused: 7/7 passed.
- Bandle Soldier active-entry / FullGameEndToEnd / MatchRecovery adjacent regression: 2456/2456 passed.
- Backend full after the StaticAbilitySpec slice: 8881/8881 passed.
- Backend full after the Dunehorn low-hand active-entry B0 official-deck replay follow-up: 8882/8882 passed.
- Backend full after the Molten Drake other-friendly active-entry B0 official-deck replay follow-up: 8883/8883 passed.
- Backend full after the Master Yi level active-entry B0 official-deck replay follow-up: 8884/8884 passed.
- Backend full after the Flameclaw level-gated source-unit active-entry + source-object static-power B0 official-deck replay follow-up: 8971/8971 passed.
- Backend full after the Bandle Soldier level-gated source-unit active-entry B0 official-deck replay follow-up: 8982/8982 passed.
- DevUi catalog type build after adding `StaticAbilitySpec.RequiredPlayerExperience` and `StaticAbilitySpec.MaxControllerHandCount`: passed.

## Remaining Evidence Needed

- Broader active-entry static ability families remain open: battlefield token entry payload coverage, turn-scoped active-entry effects, and source-zone / battlefield-entry variants beyond the currently covered other-friendly, filtered-token, friendly-unit level, source-unit low-hand, and source-unit level-gated representatives.
- Project remains NOT READY.
