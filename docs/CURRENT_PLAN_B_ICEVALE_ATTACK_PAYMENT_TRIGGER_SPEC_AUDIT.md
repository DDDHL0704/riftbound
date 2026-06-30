# Plan B Icevale Attack-Payment TriggerSpec Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice migrates the existing Icevale Archer attack-payment representative from a Core-owned source-effect selector to `BehaviorSpec.Triggers`.

The wire-compatible trigger/effect string `ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1` remains stable in payment reasons, events, and continuous-effect metadata. The old play-row source effect `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT` remains catalog row identity data, but `CoreRuleEngine` no longer consumes it to decide whether the source unit can open or resolve the attack-payment trigger.

## Authority

- `data/official/card-catalog.zh-CN.json` row `UNL-065/219` Icevale Archer / 冰谷弓箭手: `当我进攻时，你可以选择支付{{1}}，以此让此处的一名单位在本回合内{{S}}-1。`
- Existing Stage 4C-25 and Plan B trigger-payment evidence records the representative attack declaration, target selection, `TRIGGER_PAYMENT` / `PAY_COST`, payment decline, and temporary power modifier path.

## Implementation

- `TriggerKinds.UnitAttackPayPowerModifier` and `TriggerTimings.UnitAttack` define the generic attack-payment power-modifier trigger family.
- `RuleTextParser` parses Icevale's official text into a `TriggerSpec` with:
  - `Kind=UNIT_ATTACK_PAY_POWER_MODIFIER`
  - `Timing=UNIT_ATTACK`
  - `TargetScope=UNIT_AT_THIS_BATTLEFIELD`
  - `ManaCost=1`
  - `PowerDelta=-1`
  - `Duration=UNTIL_END_OF_TURN`
  - `Optional=true`
- `CardBehaviorDefinition.UnitAttackPayPowerModifierEffectKind` carries the stable runtime effect kind for implemented rows.
- `CardBehaviorRegistry.TriggerEffectKinds(...)` projects `ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1` onto the parsed Icevale trigger.
- `UnitTriggerPaymentSpecRules.TryGetUnitAttackPayPowerModifierTrigger(...)` validates source card trigger shape from the catalog-backed spec map.
- `UnitTriggerPaymentSpecRules.TryGetUnitAttackPayPowerModifierTriggerByEffectKind(...)` validates pending payment reasons without a Core-owned Icevale constant.
- `CoreRuleEngine.TryGetIcevaleArcherAttackSource(...)` now requires the parsed trigger plus the existing face-up, non-standby, controlled/legacy-owned, on-field source guards.
- Icevale payment opening and resolution now read `ManaCost`, `PowerDelta`, and effect kind from `TriggerSpec`.
- `CoreRuleEngine` no longer defines or references `IcevaleArcherAttackPaymentSourceEffectKind` or `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`.

## Validation

- Red guard failed first because `TriggerKinds.UnitAttackPayPowerModifier` and `TriggerTimings.UnitAttack` did not exist.
- Focused gate passed `2/2`:
  - `BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers`
  - `TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists`
- Adjacent Icevale / TriggerPayment / BattleDamageAssignmentLifecycle / PaymentEngine / MatchRecovery / CardCatalog gate passed `3233/3233`.
- Backend full conformance passed `9036/9036`.

## Holdbacks

This does not close complete attack-trigger family breadth, complete battle lifecycle, complete target-prompt UI breadth, full PaymentEngine / PAY_COST breadth, full official card-matrix readiness, P0/P1, or READY.
