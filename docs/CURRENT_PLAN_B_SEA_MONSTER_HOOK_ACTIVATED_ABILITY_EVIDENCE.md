# Plan B Sea Monster Hook Activated Ability Evidence

Date: 2026-07-06

Project status: **NOT READY**.

## Evidence Basis

Authority is the official catalog row for `OGN·242/298` Sea Monster Hook / 海兽钓钩 in `data/official/card-catalog.zh-CN.json`.

The implemented representative shape follows the official text fields:

- cost: pay `{{1}}` and `{{黄色}}`, exhaust source;
- target/effect: destroy one friendly unit;
- private look: top five cards of the controller's main deck;
- play option: unit card with power at most destroyed unit power plus 1, ignoring cost;
- cleanup: recycle the remaining looked cards.

## Engine Evidence

Before this slice:

- Sea Monster Hook only had 0-target equipment play guard evidence.
- The activated ability remained explicitly open in server audit text.
- `ActivatedAbilityParser` could not structure this activated ability.
- `P4ActivatedAbilityCatalog` could not expose it from BehaviorSpec.

After this slice:

- `BehaviorSpec.ActivatedAbilities` records `ManaCost=1`, `PowerCost=1`, `PowerCostTrait=yellow`, `RequiredTargetCount=1`, `TargetScope=FRIENDLY_UNIT`, `RequiresBaseEquipmentSource=true`, `MainDeckLookCount=5`, `PlayPowerDelta=1`, `IgnorePlayManaCost=true`, `RecycleUnplayedLookedCards=true`, and `PlayCardFilter=CARD_TYPE:UNIT`.
- The runtime catalog row is derived from BehaviorSpec and uses typed yellow power through `PowerCostByTrait`.
- The prompt exposes only legal friendly unit targets and hides source/equipment targets.
- The stack resolver destroys the target, tracks `DestroyedUnitOwnerIdsThisTurn`, plays exactly one eligible top-five unit when unique, and recycles the rest without public `cardIds` payload.

## Test Evidence

- Focused red/green gate passed `16/16`:
  - `CardCatalogBaselineTests.BehaviorSpecCatalogParsesSeaMonsterHookActivatedAbility`
  - `SeaMonsterHookGuardTests.SeaMonsterHookActivatedAbilityPromptIsBehaviorSpecDriven`
  - `SeaMonsterHookGuardTests.SeaMonsterHookActivatedAbilityDestroysFriendlyUnitPlaysUniqueEligibleTopFiveUnitAndRecyclesRest`
- Adjacent PaymentEngine / catalog / recovery / full-game gate is tracked through `PaymentEngineCoverageAuditTests` manifest updates for the new runtime ability row.

## Non-Claims

This does not claim full official Sea Monster Hook behavior. The multi-eligible hidden choice prompt, complete optional play decision surface, complete FAQ disposition, full hidden-zone UX, full card-matrix closure, P0/P1, and READY remain open.
