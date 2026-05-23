# Stage 4D-07A Jhin Movement Resource Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / Jhin movement resource generation and `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers Jhin's server-captured movement resource skill and the later pending `PAY_COST` window that spends its generated mana plus payment-only temporary power.

The accepted coverage proves that resolving the movement trigger emits a tightly correlated `TRIGGER_RESOLVED`, `ABILITY_ACTIVATED`, `MANA_GAINED` and `POWER_GAINED` audit chain: the activation records the movement trigger, origin / destination, generated mana / power, payment id, payment-only lifecycle and temporary payment-resource id; the mana event records the rune-pool lifecycle; and the power event records the temporary-resource ledger, restriction and allowed payment kind. The later pending `PAY_COST` prompt now directly quotes the temporary resource in `paymentResourceActionIds` and `paymentResourcePowerByChoice`, then accepted payment spends and clears the temporary resource once, maps it into `COST_PAID.paymentResourceActions`, records submitted / legal choice ids and closes the payment window.

No runtime behavior change was required because the existing Jhin movement-resource implementation and pending PaymentEngine already emitted the prompt metadata, temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `JhinMovementResourceSkillGainsManaAndPaymentOnlyPowerWithoutStackResponse` in `tests/Riftbound.ConformanceTests/JhinMovementResourceSkillTests.cs`.
- Strengthened `JhinGeneratedManaAndPowerCanPayLaterLegalRuneCostThenClear` in `tests/Riftbound.ConformanceTests/JhinMovementResourceSkillTests.cs`.
- Generation coverage now asserts event order and audit payloads for trigger resolution, activation, generated mana and generated payment-only power.
- Payment coverage now asserts prompt quote metadata, temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping and `PAYMENT_WINDOW_CLOSED` correlation.

## Non-Closure

This narrows Jhin movement-resource generation and later pending `PAY_COST` quote-command-audit parity only. It does not close full PaymentEngine / PAY_COST breadth, all movement/resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
