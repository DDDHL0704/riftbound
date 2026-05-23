# Stage 4D-07E Ancient Stele Conversion Resource Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / conversion-generated generic temporary-resource `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers Ancient Stele after its resource skill converts mana into a generic payment-only temporary resource, and the later pending `PAY_COST` window that spends that converted resource for a generic rune cost.

The accepted coverage proves the pending payment prompt quotes the converted temporary resource with stable server metadata: `paymentResourceChoices` includes the temporary action, `paymentResourceActionIds` exposes the same action id, and `paymentResourcePowerByChoice` identifies converted generic power, payment-only status and empty typed power-by-trait metadata. The accepted payment path now directly proves the converted temporary ledger is spent and cleared once, emits spend / clear / cost / window-close events in order, correlates each event by payment id and window, maps the temporary resource action into `COST_PAID.paymentResourceActions`, preserves submitted and legal payment choice ids, accounts for converted generic power, and leaves no remaining generic or typed power after the payment window closes.

No runtime behavior change was required because the existing resource-conversion implementation and pending PaymentEngine already emitted the prompt metadata, temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `AncientSteleTemporaryGenericResourcePaysGenericRuneCostButRejectsManaOnly` in `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`.
- Prompt coverage now asserts converted generic temporary-resource quote metadata across `paymentResourceChoices`, `paymentResourceActionIds` and `paymentResourcePowerByChoice`.
- Payment coverage now asserts event order, temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, submitted / legal payment choices, converted generic power accounting and `PAYMENT_WINDOW_CLOSED` correlation.

## Non-Closure

This narrows Ancient Stele conversion-generated temporary-resource quote-command-audit parity only. It does not close full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
