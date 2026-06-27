# Plan B / Ambush Interaction Spec Evidence

Date: 2026-06-27

## Official Inputs

- Official catalog rows: `UNL-021/219` 阴森药剂师 and `UNL-176/219` 蔚 both expose `伏击`.
- Rules evidence index anchors remain `CORE-260330` p39-p42 rules 355-356, p57 rule 413.4, and p92-p105 keyword rules 800+ for play timing, stack priority, and keyword surfaces.

## Engine Evidence

- Added `src/Riftbound.Engine/AmbushInteractionSpecRules.cs`.
- Removed `CoreRuleEngine.GloomyApothecaryCardNo`.
- `CoreRuleEngine.TryBuildMinimalAmbushPlayCardPlan(...)` now reads Ambush permission from `BehaviorSpec` through `AmbushInteractionSpecRules.HasAmbush(command.CardNo)`.
- The narrow path still delegates cost, effect kind, and source-unit resolution to existing `CardBehaviorRegistry` rows and still requires the live source object to carry the `伏击` tag.

## Test Evidence

- `CardCatalogBaselineTests.AmbushReactionPlayDoesNotUseCardNumberAllowList` locks the Core entry point against reintroducing `GloomyApothecaryCardNo` and requires the BehaviorSpec rule call.
- `CardCatalogBaselineTests.P4InteractionKeywordProfilesMapOfficialTextToRegistryTags` now checks both `UNL-021/219` and `UNL-176/219` Ambush keyword mapping.
- `ConformanceFixtureRunnerTests.P4AmbushPlayCardModeInPriorityWindowPlaysBehaviorSpecAmbushUnitToBattlefield` proves another official Ambush unit can enter the same minimal battlefield reaction stack path without a Core card-number branch.
- Existing Ambush rejection fixtures and tests continue covering source outside hand, unknown source, opponent hand source, non-Ambush source, card-number mismatch, target payload, optional-cost payload, base destination, and no friendly battlefield unit.

## Remaining Risk

This slice proves BehaviorSpec-driven Ambush permission for the existing minimal no-target battlefield stack entry. It does not close complete Ambush target/rider semantics, complete destination coordinates, or full official Ambush card breadth.
