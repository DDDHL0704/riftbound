# Plan B TriggerSpec And Behavior-Field Cleanup Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `SFD·155/221` Honest Broker has a last-breath Gold token trigger.
- `OGN·264/298` Guerrilla Warfare is the source of the existing free standby-hide permission after returning standby graveyard cards.

Existing engine evidence:

- `BehaviorSpecCatalogParsesUnitLastBreathCreateDormantGoldTrigger` proves Honest Broker parses to `TriggerKinds.UnitLastBreathCreateDormantGold` with `Timing=UNIT_DESTROYED`, `TargetScope=SOURCE_UNIT`, one dormant Gold equipment token, owner-base destination, and exhausted token state.
- `RealTriggerQueueTests` and fixture runner tests cover Honest Broker trigger queue ordering, hidden-source rejection, stack resolution, and Gold token creation.
- Existing Guerrilla Warfare fixture runner tests cover returning standby graveyard cards and using the granted free standby-hide optional cost.

## Engine Evidence

Honest Broker:

- Before this slice, `CoreRuleEngine.ResolveHonestBrokerLastBreathGoldPlayerId(...)` used the Core constant `HonestBrokerLastBreathSourceEffectKind = HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT` to select the source row.
- After this slice, `CoreRuleEngine` no longer contains `HonestBrokerLastBreathSourceEffectKind`.
- The source predicate now reads `UnitDestroyedTriggerSpecRules.TryGetTrigger(..., IsLastBreathCreateDormantGoldTrigger, ...)` and validates the parsed TriggerSpec shape.
- Trigger queue and stack effect kind remain `HONEST_BROKER_LAST_BREATH_CREATE_GOLD`.

Guerrilla Warfare:

- Before this slice, `CoreRuleEngine.ShouldGrantFreeStandbyHidePermission(...)` compared `behavior.EffectKind` to the Core constant `GuerrillaWarfareEffectKind`.
- After this slice, `CoreRuleEngine` no longer contains `GuerrillaWarfareEffectKind`.
- `CardBehaviorRegistry` stores `GrantsFreeStandbyHidePermission=true` on `OGN·264/298`.
- `CoreRuleEngine.ShouldGrantFreeStandbyHidePermission(...)` reads `behavior.GrantsFreeStandbyHidePermission`.
- Event payloads still expose the played spell effect kind through existing stack behavior and fixture expectations.

## Test Evidence

- `CardCatalogBaselineTests.UnitLastBreathCreateDormantGoldTriggerDoesNotUseCoreCardNumberBehavior` now blocks `HonestBrokerLastBreathSourceEffectKind`.
- `CardCatalogBaselineTests.GuerrillaWarfareFreeStandbyPermissionUsesBehaviorField` locks the `OGN·264/298` behavior field and blocks `GuerrillaWarfareEffectKind` in Core.
- Honest Broker red gate failed before implementation on the Core constant; focused passed `10/10`; adjacent passed `2400/2400`.
- Guerrilla Warfare red gate failed before implementation on the missing behavior field; focused passed `8/8`; adjacent passed `1266/1266`.
- Backend full conformance passed `9036/9036`.

## Non-Claims

This evidence does not claim complete last-breath family breadth, complete standby/hidden payment breadth, complete TriggerSpec migration for every trigger family, P0 completion, P1, or READY.
