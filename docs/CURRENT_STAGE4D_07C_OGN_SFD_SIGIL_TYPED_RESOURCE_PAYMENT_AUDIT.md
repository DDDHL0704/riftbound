# Stage 4D-07C OGN/SFD Sigil Typed Resource Payment Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / OGN and SFD Sigil typed temporary-resource `PAY_COST` quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers the OGN and SFD Sigil profile families after their reaction resource skills create profile-specific typed payment-only temporary resources, and the later pending `PAY_COST` windows that may spend them for either same-color typed rune costs or generic rune costs.

The accepted coverage proves each pending payment prompt quotes the temporary resource with stable server metadata: `paymentResourceChoices` includes the temporary action, `paymentResourceActionIds` exposes the same action id, and `paymentResourcePowerByChoice` identifies the action as zero generic power, profile-trait typed power, payment-only and profile trait. The accepted payment path now directly proves both typed and generic cost windows spend and clear each typed temporary ledger once, emit spend / clear / cost / window-close events in order, correlate each event by payment id and window, map the temporary resource action into `COST_PAID.paymentResourceActions`, preserve submitted and legal payment choice ids, and leave no remaining generic or typed power.

No runtime behavior change was required because the existing OGN/SFD sigil resource-skill implementation and pending PaymentEngine already emitted the prompt metadata, temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `OgnSigilTemporaryTypedResourcePaysSameColorAndGenericRuneCosts` in `tests/Riftbound.ConformanceTests/OgnSigilResourceSkillTests.cs`.
- Strengthened `SfdSigilTemporaryTypedResourcePaysSameColorAndGenericRuneCosts` in `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`.
- Prompt coverage now asserts typed temporary-resource quote metadata across `paymentResourceChoices`, `paymentResourceActionIds` and `paymentResourcePowerByChoice`.
- Payment coverage now asserts event order, typed temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, submitted / legal payment choices, typed power accounting and `PAYMENT_WINDOW_CLOSED` correlation.

## Non-Closure

This narrows OGN/SFD Sigil typed temporary-resource quote-command-audit parity only. It does not close full PaymentEngine / PAY_COST breadth, all typed resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
