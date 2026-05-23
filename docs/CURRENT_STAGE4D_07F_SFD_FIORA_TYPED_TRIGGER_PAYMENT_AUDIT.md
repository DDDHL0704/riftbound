# Stage 4D-07F SFD Fiora Typed Trigger Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / typed temporary-resource trigger `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers the SFD Fiora trigger payment window when a Unity Sigil typed yellow payment-only temporary resource is available and submitted to pay the trigger's yellow power cost.

The accepted coverage proves the trigger payment spends and clears the typed yellow temporary ledger once before the cost commit, correlates spend / clear / cost / window-close events by payment id and window, maps the temporary action into `COST_PAID.paymentResourceActions`, preserves submitted and legal payment choice ids, accounts for typed yellow power in `TEMPORARY_PAYMENT_RESOURCE_SPENT`, `temporaryPaymentResourcePowerByTrait` and `powerByTrait`, closes the payment window, and then resolves the Fiora ready trigger.

No runtime behavior change was required because the existing trigger-payment and PaymentEngine paths already emitted the temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `SfdFioraPaymentConsumesTypedYellowTemporaryPaymentResource` in `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`.
- Payment coverage now asserts relative event order, temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, submitted / legal payment choices, typed yellow accounting, `PAYMENT_WINDOW_CLOSED` correlation and Fiora trigger resolution order.

## Non-Closure

This narrows SFD Fiora typed temporary-resource trigger-payment parity only. It does not close full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
