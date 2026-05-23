# Stage 4D-07B Rage Sigil Typed Resource Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / Rage Sigil typed temporary-resource `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers Rage Sigil's red typed payment-only temporary resource after it is created by the reaction resource skill, and the later pending `PAY_COST` window that may spend it for either an explicit red typed rune cost or a generic rune cost.

The accepted coverage proves the pending payment prompt quotes the temporary resource with stable server metadata: `paymentResourceChoices` includes the temporary action, `paymentResourceActionIds` exposes the same action id, and `paymentResourcePowerByChoice` identifies the action as zero generic power, red typed power, payment-only and red trait. The accepted payment path now directly proves both red-typed and generic cost windows spend and clear that one red typed temporary ledger once, emit spend / clear / cost / window-close events in order, correlate each event by payment id and window, map the temporary resource action into `COST_PAID.paymentResourceActions`, preserve submitted and legal payment choice ids, and leave no remaining generic or typed power.

No runtime behavior change was required because the existing Rage Sigil resource-skill implementation and pending PaymentEngine already emitted the prompt metadata, temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `RageSigilTemporaryRedResourcePaysRuneCostsAndCleansUp` in `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`.
- Prompt coverage now asserts temporary-resource quote metadata across `paymentResourceChoices`, `paymentResourceActionIds` and `paymentResourcePowerByChoice`.
- Payment coverage now asserts event order, typed temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, submitted / legal payment choices, red typed power accounting and `PAYMENT_WINDOW_CLOSED` correlation.

## Non-Closure

This narrows Rage Sigil typed temporary-resource quote-command-audit parity only. It does not close full PaymentEngine / PAY_COST breadth, all typed resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
