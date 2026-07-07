# Plan B Sea Monster Hook Activated Ability Evidence

Date: 2026-07-07

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
- The B0 full-game replay path now proves the same activated ability can be driven from an official-deck-derived state, recorded in the action log, replayed to the final state hash, and carried onward to score victory.
- `PendingCardChoiceState` now opens a controller-only `CARD_CHOICE` prompt when multiple eligible top-five units exist. `CHOOSE_CARDS` accepts one selected eligible unit or an empty choice, then recycles the unplayed looked cards with `visibility=LOOKED_NOT_REVEALED` and no public `cardIds`.
- The B0 multi-eligible replay path now proves that the private `CARD_CHOICE` / `CHOOSE_CARDS` branch is recorded with complete raw payload, replays to the same final state hash, and can continue to score victory without exposing the looked card ids to the non-choosing player.

## Test Evidence

- Focused red/green gate passed `16/16`:
  - `CardCatalogBaselineTests.BehaviorSpecCatalogParsesSeaMonsterHookActivatedAbility`
  - `SeaMonsterHookGuardTests.SeaMonsterHookActivatedAbilityPromptIsBehaviorSpecDriven`
  - `SeaMonsterHookGuardTests.SeaMonsterHookActivatedAbilityDestroysFriendlyUnitPlaysUniqueEligibleTopFiveUnitAndRecyclesRest`
- B0 replay follow-up passed `1/1`:
  - `FullGameEndToEndTests.OfficialDeckMidgameResolvesSeaMonsterHookActivatedAbilityAndScoreVictoryActionLogReplaysToFinalStateHash`
- B0 multi-eligible replay follow-up passed `1/1`:
  - `FullGameEndToEndTests.OfficialDeckMidgameResolvesSeaMonsterHookMultiEligibleCardChoiceAndScoreVictoryActionLogReplaysToFinalStateHash`
- Multi-eligible / decline follow-up passed in focused SeaMonsterHook guard `12/12`:
  - `SeaMonsterHookGuardTests.SeaMonsterHookActivatedAbilityWithMultipleEligibleTopFiveUnitsPromptsControllerToChoosePrivately`
  - `SeaMonsterHookGuardTests.SeaMonsterHookActivatedAbilityTopFiveChoiceCanDeclineAndRecycleAllLookedCardsPrivately`
- Focused SeaMonsterHook + MatchRecovery hidden-info regression passed `2001/2001`.
- Adjacent SeaMonsterHook / CardChoice / ChooseCards / FullGameEndToEnd / MatchRecovery / PaymentEngine regression passed `2924/2924`.
- Backend full conformance passed `9194/9194`.
- Adjacent PaymentEngine / catalog / recovery / full-game coverage is tracked through `PaymentEngineCoverageAuditTests` manifest updates for the new runtime ability row.

## Non-Claims

This does not claim full official Sea Monster Hook behavior. Complete FAQ disposition, zero-eligible edge matrix, full hidden-zone UX across all clients/recovery, full card-matrix closure, P0/P1, and READY remain open.
