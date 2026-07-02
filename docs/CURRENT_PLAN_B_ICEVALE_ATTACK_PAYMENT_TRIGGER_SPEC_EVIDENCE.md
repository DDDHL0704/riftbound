# Plan B Icevale Attack-Payment TriggerSpec Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Official Evidence

- Official catalog row `UNL-065/219` Icevale Archer / 冰谷弓箭手 says: `当我进攻时，你可以选择支付{{1}}，以此让此处的一名单位在本回合内{{S}}-1。`
- No official data file was edited.

## Runtime Evidence

- `RuleTextParser` recognizes the Icevale attack-payment text and emits `TriggerSpec` data for attack timing, same-battlefield target scope, optional mana payment, until-end-of-turn duration, and -1 power delta.
- `CardBehaviorRegistry` stores only the stable trigger effect metadata for Icevale through `UnitAttackPayPowerModifierEffectKind`.
- `BehaviorSpecCatalogBuilder` projects that metadata into `TriggerSpec.EffectKind`.
- `UnitTriggerPaymentSpecRules` consumes the same catalog-backed `BehaviorSpec` map at runtime and validates both source-card trigger shape and pending-payment effect kind through `TryGetTrigger(..., IsUnitAttackPayPowerModifierTrigger, ...)` and `TryGetTriggerByEffectKind(..., IsUnitAttackPayPowerModifierTrigger, ...)`.
- `CoreRuleEngine` opens and resolves the Icevale attack-payment window from `TriggerSpec` instead of `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`.
- Existing event and snapshot compatibility remains stable:
  - payment reason first segment: `ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1`
  - `PAYMENT_WINDOW_OPENED.trigger`
  - `BATTLEFIELD_TRIGGER_RESOLVED.trigger`
  - `POWER_MODIFIED_UNTIL_END_OF_TURN.reason`
  - power modifier ledger effect kind

## Test Evidence

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers` now proves `UNL-065/219` parses to `UNIT_ATTACK_PAY_POWER_MODIFIER` with the expected timing, target scope, cost, power delta, duration, optionality, and effect kind.
- `TriggerPaymentTests.TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists` now requires the generic UnitTriggerPayment predicate route and blocks `IcevaleArcherAttackPaymentSourceEffectKind` / `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT` from returning to `CoreRuleEngine`.
- 2026-07-02 predicate-surface focused guard / representative runtime set passed `52/52`; adjacent TriggerPayment / PaymentEngine / MatchRecovery set passed `3241/3241`; backend full conformance passed `9141/9141`.
- Existing Icevale payment tests continue to cover prompt opening, payment acceptance, decline, replay rejection, invalid target rejection, and hidden/source guard suppression.
- Existing battle-damage lifecycle tests continue to cover Icevale post-payment battle-response advancement and no-mutation rejection behavior.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists"
```

Result: `2/2` passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~Icevale|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattleDamageAssignmentLifecycle|FullyQualifiedName~PaymentEngine|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result: `3233/3233` passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: `9036/9036` passed.

## Non-Closure

This evidence does not close complete attack-trigger family breadth, complete battle lifecycle, complete target-prompt UI breadth, complete hidden-info matrix, full PaymentEngine / PAY_COST breadth, full official card-matrix readiness, frontend final validation, formal E2E, P0/P1, or READY.
