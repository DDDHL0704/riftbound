# Plan B TriggerSpec And Behavior-Field Cleanup Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes two remaining Core-owned behavior-selection constants from shared gameplay paths:

- Honest Broker last-breath Gold source selection now reads `UNIT_LAST_BREATH_CREATE_DORMANT_GOLD` from `UnitDestroyedTriggerSpecRules`.
- Guerrilla Warfare free standby-hide permission now reads `CardBehaviorDefinition.GrantsFreeStandbyHidePermission`.

No public trigger id, event payload, fixture payload, recovery payload, target legality, payment window, or snapshot shape is intentionally changed.

## Authority

- `data/official/card-catalog.zh-CN.json` row `SFD·155/221` Honest Broker: last-breath creates one dormant Gold equipment token.
- `data/official/card-catalog.zh-CN.json` row `OGN·264/298` Guerrilla Warfare: returns friendly standby cards from graveyard to hand and grants the free standby-hide permission used by the existing server path.
- Existing evidence in `docs/CURRENT_SERVER_RULE_AUDIT.md` records the Honest Broker last-breath trigger queue and Guerrilla Warfare free standby-hide representatives.

## Implementation

- `CoreRuleEngine.ResolveHonestBrokerLastBreathGoldPlayerId(...)` no longer checks `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT`.
- The Honest Broker source predicate now requires a face-up non-standby unit plus a parsed `UNIT_LAST_BREATH_CREATE_DORMANT_GOLD` TriggerSpec with source-unit timing/target/token shape.
- `CoreRuleEngine.TryGetLastBreathCreateDormantGoldTrigger(...)` now validates the same TriggerSpec shape before creating Gold tokens from trigger stack items.
- `CardBehaviorDefinition` now has `GrantsFreeStandbyHidePermission`.
- `CardBehaviorRegistry` marks `OGN·264/298` Guerrilla Warfare with `GrantsFreeStandbyHidePermission=true`.
- `CoreRuleEngine.ShouldGrantFreeStandbyHidePermission(...)` now reads that behavior field instead of comparing `behavior.EffectKind` to a Core constant.

## Validation

- Honest Broker red gate failed first on `HonestBrokerLastBreathSourceEffectKind` still being present in `CoreRuleEngine`.
- Honest Broker focused gate passed `10/10`.
- Last-breath / trigger queue / recovery adjacent gate passed `2400/2400`.
- Guerrilla Warfare red gate failed first because `CardBehaviorDefinition.GrantsFreeStandbyHidePermission` did not exist.
- Guerrilla Warfare focused gate passed `8/8`.
- HideCard / standby / PaymentEngine / catalog adjacent gate passed `1266/1266`.
- Backend full conformance passed `9036/9036`.

## Holdbacks

This does not close complete last-breath family breadth, complete standby/hidden payment breadth, complete TriggerSpec migration for every remaining trigger family, P0 full objective, P1, or READY.
