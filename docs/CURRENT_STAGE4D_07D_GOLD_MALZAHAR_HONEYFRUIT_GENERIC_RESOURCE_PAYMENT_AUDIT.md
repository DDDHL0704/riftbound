# Stage 4D-07D Gold/Malzahar/Honeyfruit Generic Resource Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / generic temporary-resource `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers Gold Token, Malzahar and Honeyfruit after their resource skills create generic payment-only temporary resources, and the later pending `PAY_COST` windows that may spend those resources for generic rune costs.

The accepted coverage proves each pending payment prompt quotes the temporary resource with stable server metadata: `paymentResourceChoices` includes the temporary action, `paymentResourceActionIds` exposes the same action id, and `paymentResourcePowerByChoice` identifies generated generic power, payment-only status and empty typed power-by-trait metadata. The accepted payment path now directly proves Gold Token, Malzahar and Honeyfruit generic resource windows spend and clear each temporary ledger once, emit spend / clear / cost / window-close events in order, correlate each event by payment id and window, map the temporary resource action into `COST_PAID.paymentResourceActions`, preserve submitted and legal payment choice ids, account for generated generic power, and leave no remaining generic or typed power after the payment window closes.

No runtime behavior change was required because the existing resource-skill implementation and pending PaymentEngine already emitted the prompt metadata, temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `GoldTemporaryGenericResourcePaysGenericRuneCostAndCleansUp` in `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`.
- Strengthened `MalzaharTemporaryPaymentResourcePaysRuneCostAndCleansUpAtPaymentClose` in `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`.
- Strengthened `HoneyfruitGeneratedResourcesPayLaterRuneCostAndClearAtPaymentOrTurnCleanup` in `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs`.
- Prompt coverage now asserts generic temporary-resource quote metadata across `paymentResourceChoices`, `paymentResourceActionIds` and `paymentResourcePowerByChoice`.
- Payment coverage now asserts event order, temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, submitted / legal payment choices, generated generic power accounting and `PAYMENT_WINDOW_CLOSED` correlation.

## Non-Closure

This narrows Gold/Malzahar/Honeyfruit generic temporary-resource quote-command-audit parity only. It does not close full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
