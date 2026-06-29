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

Controlled-tag source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilitySpec.RequiredOtherControlledUnitTag`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `如果你控制着其他“龙”属性单位，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredOtherControlledUnitTag=龙`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` also parses `如果你控制着其他“机械”单位，则我以活跃状态进场。` into the same `SOURCE_UNIT_ENTER_READY` shape with `RequiredOtherControlledUnitTag=机械`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `requiredOtherControlledUnitTag` on `staticAbilities` for the shared catalog payload shape.
- `tests/Riftbound.ConformanceTests/ControlledTaggedSourceUnitActiveEntryStaticAbilityTests.cs` verifies `SFD·094/221` 凶翼 and `SFD·071/221` 疾驰机械 expose this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Controlled-tag source-unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetSourceUnitEnterReadyAbility` for source-unit active-entry specs that carry `MaxControllerHandCount`, `RequiredPlayerExperience`, `RequiredOtherControlledUnitTag`, or `RequiredOpponentControlledBattlefieldCount`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through `StaticAbilitySpec.Kind=SOURCE_UNIT_ENTER_READY` and checks the entering unit controller's public field objects for another controlled, face-up, non-standby unit with the parsed official tag.
- `tests/Riftbound.ConformanceTests/ControlledTaggedSourceUnitActiveEntryStaticAbilityTests.cs` proves Fiercewing enters ready when its controller controls another public `龙` unit and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves no other public controlled tagged unit leaves Fiercewing exhausted: no other unit, a friendly face-down standby Dragon, and an opponent-controlled face-up Dragon do not satisfy the parsed requirement and emit no entry-static metadata.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` now proves a legal official Poppy deck opening can feed `SFD·094/221` Fiercewing controlled-Dragon active-entry into B0 score victory: P1 first plays official `OGN·131/298` Dune Drake to base as a public controlled Dragon, then plays Fiercewing directly to a battlefield, receives `SOURCE_UNIT_ENTER_READY` self metadata, enters active with printed 7 power, and replays the same action log to the final score-victory state hash without hidden-zone leaks.

Opponent-battlefield source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilitySpec.RequiredOpponentControlledBattlefieldCount`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `requiredOpponentControlledBattlefieldCount` on `staticAbilities` for the shared catalog payload shape.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `如果对手已控制任意战场，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredOpponentControlledBattlefieldCount=1`.
- `tests/Riftbound.ConformanceTests/OpponentBattlefieldSourceUnitActiveEntryStaticAbilityTests.cs` verifies `OGN·035/298`, `SFD·223/221`, and `SFD·223*/221` 薇恩 expose this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Opponent-battlefield source-unit active-entry runtime:

- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through the same `SOURCE_UNIT_ENTER_READY` requirement gate and counts opponent-controlled public battlefield-card objects.
- The battlefield requirement uses the shared battlefield-card predicate/tag path, so a unit object merely located in an opponent battlefield zone does not satisfy `RequiredOpponentControlledBattlefieldCount`.
- `tests/Riftbound.ConformanceTests/OpponentBattlefieldSourceUnitActiveEntryStaticAbilityTests.cs` proves Vayne enters ready when the opponent controls a public battlefield card and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves no opponent battlefield card leaves Vayne exhausted: no battlefield objects and an opponent-controlled unit at a battlefield do not satisfy the parsed requirement and emit no entry-static metadata.

Unit-destroyed-this-turn source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilitySpec.RequiresUnitDestroyedThisTurn`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `requiresUnitDestroyedThisTurn` on `staticAbilities` for the shared catalog payload shape.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `如果本回合内有单位被摧毁，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiresUnitDestroyedThisTurn=true`.
- `tests/Riftbound.ConformanceTests/UnitDestroyedThisTurnSourceUnitActiveEntryStaticAbilityTests.cs` verifies `UNL-008/219` 莽林巨象 exposes this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Unit-destroyed-this-turn source-unit active-entry runtime:

- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through the same `SOURCE_UNIT_ENTER_READY` requirement gate and checks `DestroyedUnitOwnerIdsThisTurn`.
- The requirement is owner-agnostic: either player's destroyed unit owner id satisfies the official `有单位被摧毁` wording.
- `tests/Riftbound.ConformanceTests/UnitDestroyedThisTurnSourceUnitActiveEntryStaticAbilityTests.cs` proves Jungle Elephant enters ready when a P1-owned or P2-owned unit was destroyed earlier this turn and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves no destroyed unit this turn leaves Jungle Elephant exhausted and emits no entry-static metadata.

Controller-base unit-count source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilitySpec.RequiredOtherControllerBaseUnitCount`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `requiredOtherControllerBaseUnitCount` on `staticAbilities` for the shared catalog payload shape.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `如果你的基地中有不少于两名其他单位，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredOtherControllerBaseUnitCount=2`.
- `tests/Riftbound.ConformanceTests/OtherBaseUnitsSourceUnitActiveEntryStaticAbilityTests.cs` verifies `SFD·176/221` 赵信 exposes this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Controller-base unit-count source-unit active-entry runtime:

- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through the same `SOURCE_UNIT_ENTER_READY` requirement gate and counts other public controller base units.
- The count excludes the entering source object and ignores face-down standby objects or opponent-controlled base units.
- `tests/Riftbound.ConformanceTests/OtherBaseUnitsSourceUnitActiveEntryStaticAbilityTests.cs` proves Xin Zhao enters ready when the controller base has two other public units and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves one friendly base unit, one friendly plus one opponent base unit, or one public friendly plus one face-down standby friendly object leaves Xin Zhao exhausted and emits no entry-static metadata.

Battlefield-destination source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `StaticAbilitySpec.RequiresBattlefieldDestination`.
- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors `requiresBattlefieldDestination` on `staticAbilities` for the shared catalog payload shape.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `如果你将我打出至一处战场，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiresBattlefieldDestination=true`.
- `tests/Riftbound.ConformanceTests/BattlefieldDestinationSourceUnitActiveEntryStaticAbilityTests.cs` verifies `UNL-194/219` 黑影 exposes this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Battlefield-destination source-unit active-entry runtime:

- `src/Riftbound.Engine/CoreRuleEngine.cs` routes source-unit play entry through the same `SOURCE_UNIT_ENTER_READY` requirement gate and checks whether the stack item destination is a battlefield.
- The requirement is destination-specific: `BATTLEFIELD:*` satisfies the parsed official text, while the default base destination does not.
- `tests/Riftbound.ConformanceTests/BattlefieldDestinationSourceUnitActiveEntryStaticAbilityTests.cs` proves a pre-exhausted Shadow enters ready when played to `BATTLEFIELD:P1-MAIN` and emits `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY` with self source object/card metadata.
- The same test proves playing Shadow to base leaves it exhausted and emits no entry-static metadata.

Unconditional source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses exact `我以活跃状态进场。` segments into `SOURCE_UNIT_ENTER_READY` with no requirement fields.
- `tests/Riftbound.ConformanceTests/UnconditionalSourceUnitActiveEntryStaticAbilityTests.cs` verifies `SFD·006/221` 好斗的龙犬 and `OGS·016/024` 先锋扈从 expose this unconditional source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.
- The same test verifies `UNL-006/219` 小鲨鱼 Haste reminder text does not expose a false `SOURCE_UNIT_ENTER_READY` spec from the phrase `让我以活跃状态进场`.

Unconditional source-unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` now exposes `TryGetSourceUnitEnterReadyAbility` for source-unit active-entry specs even when `MaxControllerHandCount`, `RequiredPlayerExperience`, `RequiredOtherControlledUnitTag`, and `RequiredOpponentControlledBattlefieldCount` are all null.
- `src/Riftbound.Engine/CoreRuleEngine.cs` already treated missing source-unit requirements as satisfied, so unconditional specs reuse the same source-unit entry path and event metadata as the conditional forms.
- `tests/Riftbound.ConformanceTests/UnconditionalSourceUnitActiveEntryStaticAbilityTests.cs` proves pre-exhausted Aggressive Dragonhound enters ready from its parsed unconditional `SOURCE_UNIT_ENTER_READY` spec and emits self source object/card metadata.
- `tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs` proves a legal official Rumble deck opening can feed `SFD·006/221` Aggressive Dragonhound unconditional active-entry into B0 score victory: the focused midgame pre-exhausts the source object in hand, playing it directly to a P1 battlefield resolves `SOURCE_UNIT_ENTER_READY`, emits self source metadata, enters active with printed 3 power, and replays the same action log to the final score-victory state hash without hidden-zone leaks.

Level-gated source-unit active-entry BehaviorSpec / catalog:

- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `{{等级3>}} 我获得{{S}}+1，并以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredPlayerExperience=3`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` also parses `{{等级3>}} 我以活跃状态进场。` into the same `SOURCE_UNIT_ENTER_READY` shape with `RequiredPlayerExperience=3`.
- `tests/Riftbound.ConformanceTests/SourceUnitLevelActiveEntryStaticAbilityTests.cs` verifies `UNL-016/219` 焰爪 and `UNL-151/219` 班德尔士兵 expose this source-unit active-entry spec through `BehaviorSpec.StaticAbilities`.

Level-gated source-unit active-entry runtime:

- `src/Riftbound.Engine/CardStaticAbilitySpecRules.cs` exposes `TryGetSourceUnitEnterReadyAbility` for source-unit active-entry specs that carry source-unit requirement fields such as `MaxControllerHandCount`, `RequiredPlayerExperience`, `RequiredOtherControlledUnitTag`, or `RequiredOpponentControlledBattlefieldCount`.
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
- Controlled-tag source-unit active-entry pre-implementation red: `ControlledTaggedSourceUnitActiveEntryStaticAbilityTests` initially failed at compile because `StaticAbilitySpec.RequiredOtherControlledUnitTag` did not exist.
- Controlled-tag source-unit active-entry + Fiercewing B0 official-deck replay focused: 7/7 passed.
- Fiercewing controlled-tag active-entry / FullGameEndToEnd / MatchRecovery adjacent regression: 2427/2427 passed.
- Opponent-battlefield source-unit active-entry pre-implementation red: `OpponentBattlefieldSourceUnitActiveEntryStaticAbilityTests` initially failed at compile because `StaticAbilitySpec.RequiredOpponentControlledBattlefieldCount` did not exist.
- Opponent-battlefield source-unit active-entry focused post-implementation: 6/6 passed.
- Opponent-battlefield source-unit active-entry / source-unit active-entry / Vayne trigger-payment adjacent regression: 112/112 passed.
- Opponent-battlefield source-unit active-entry / active-entry / Vayne / CardCatalogBaseline / MatchRecovery adjacent regression: 2369/2369 passed.
- Unit-destroyed-this-turn source-unit active-entry pre-implementation red: `UnitDestroyedThisTurnSourceUnitActiveEntryStaticAbilityTests` initially failed at compile because `StaticAbilitySpec.RequiresUnitDestroyedThisTurn` did not exist.
- Unit-destroyed-this-turn source-unit active-entry focused post-implementation: 4/4 passed.
- Unit-destroyed-this-turn source-unit active-entry / active-entry / CardCatalogBaseline / MatchRecovery adjacent regression: 2363/2363 passed.
- Controller-base unit-count source-unit active-entry pre-implementation red: `OtherBaseUnitsSourceUnitActiveEntryStaticAbilityTests` initially failed at compile because `StaticAbilitySpec.RequiredOtherControllerBaseUnitCount` did not exist.
- Controller-base unit-count source-unit active-entry focused post-implementation: 5/5 passed.
- Controller-base unit-count source-unit active-entry / active-entry / CardCatalogBaseline / MatchRecovery adjacent regression: 2368/2368 passed.
- Battlefield-destination source-unit active-entry pre-implementation red: `BattlefieldDestinationSourceUnitActiveEntryStaticAbilityTests` initially failed at compile because `StaticAbilitySpec.RequiresBattlefieldDestination` did not exist.
- Battlefield-destination source-unit active-entry focused post-implementation: 3/3 passed.
- Battlefield-destination source-unit active-entry / Shadow / active-entry / FullGameEndToEnd / CardCatalogBaseline / MatchRecovery adjacent regression: 2533/2533 passed.
- Unconditional source-unit active-entry pre-implementation red: `UnconditionalSourceUnitActiveEntryStaticAbilityTests` initially found no parsed `SOURCE_UNIT_ENTER_READY` spec for `SFD·006/221` / `OGS·016/024`, and Aggressive Dragonhound stayed exhausted when its source object was pre-exhausted.
- Unconditional source-unit active-entry + Aggressive Dragonhound B0 official-deck replay focused post-implementation: 5/5 passed.
- Unconditional source-unit active-entry / active-entry / FullGameEndToEnd / MatchRecovery adjacent regression: 2468/2468 passed.
- Backend full after the StaticAbilitySpec slice: 8881/8881 passed.
- Backend full after the Dunehorn low-hand active-entry B0 official-deck replay follow-up: 8882/8882 passed.
- Backend full after the Molten Drake other-friendly active-entry B0 official-deck replay follow-up: 8883/8883 passed.
- Backend full after the Master Yi level active-entry B0 official-deck replay follow-up: 8884/8884 passed.
- Backend full after the Flameclaw level-gated source-unit active-entry + source-object static-power B0 official-deck replay follow-up: 8971/8971 passed.
- Backend full after the Bandle Soldier level-gated source-unit active-entry B0 official-deck replay follow-up: 8982/8982 passed.
- Backend full after the Fiercewing controlled-tag source-unit active-entry B0 official-deck replay follow-up: 8990/8990 passed.
- Backend full after the unconditional source-unit active-entry B0 official-deck replay follow-up: 8997/8997 passed.
- Backend full after the Vayne opponent-battlefield source-unit active-entry StaticAbilitySpec follow-up: 9003/9003 passed.
- Backend full after the Jungle Elephant unit-destroyed-this-turn source-unit active-entry StaticAbilitySpec follow-up: 9010/9010 passed.
- Backend full after the Xin Zhao controller-base unit-count source-unit active-entry StaticAbilitySpec follow-up: 9015/9015 passed.
- Backend full after the Shadow battlefield-destination source-unit active-entry StaticAbilitySpec follow-up: 9018/9018 passed.
- DevUi catalog type build after adding `StaticAbilitySpec.RequiredPlayerExperience`, `StaticAbilitySpec.MaxControllerHandCount`, and `StaticAbilitySpec.RequiredOpponentControlledBattlefieldCount`: passed.
- DevUi catalog type build after adding `StaticAbilitySpec.RequiresUnitDestroyedThisTurn`: passed.
- DevUi catalog type build after adding `StaticAbilitySpec.RequiredOtherControllerBaseUnitCount`: passed.
- DevUi catalog type build after adding `StaticAbilitySpec.RequiresBattlefieldDestination`: passed.

## Remaining Evidence Needed

- Broader active-entry static ability families remain open: battlefield token entry payload coverage, turn-scoped active-entry effects, and source-zone variants beyond the currently covered other-friendly, filtered-token, friendly-unit level, source-unit unconditional, source-unit low-hand, source-unit level-gated, source-unit controlled-tag, source-unit opponent-battlefield, source-unit unit-destroyed-this-turn, source-unit controller-base unit-count, and source-unit battlefield-destination representatives.
- Project remains NOT READY.
