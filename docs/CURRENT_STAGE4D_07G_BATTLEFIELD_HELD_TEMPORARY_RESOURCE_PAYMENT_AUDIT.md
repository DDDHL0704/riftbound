# Stage 4D-07G Battlefield Held Temporary Resource Payment Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / battlefield-held temporary-resource quote-command-audit parity slice. Project remains **NOT READY**.

## Scope

This slice covers P79 battlefield-held score payment paths when a payment-only temporary resource is quoted and submitted as part of `DECLARE_BATTLE` optional costs.

The accepted coverage proves battlefield-held score prompts quote both generic Malzahar-style and typed Rage Sigil-style temporary resources through `paymentResourceChoices` and `paymentResourcePowerByChoice`, including payment-only marker, temporary resource id, resource restriction, allowed payment kind, generic power or typed `powerByTrait`, and total available power with payment resources.

Accepted `DECLARE_BATTLE` coverage now proves generic, typed-red and recycle-plus-temporary battlefield-held score payments spend and clear the temporary ledger once, correlate spend / clear / `COST_PAID` by payment id and window, map submitted temporary actions into `COST_PAID.paymentResourceActions`, preserve temporary resource ids, account for generic or typed consumed power, gain score, and keep event order from battlefield trigger resolution through score gain.

No runtime behavior change was required because the existing battlefield-held / PaymentEngine paths already emitted the temporary-resource spend/clear events and `COST_PAID` payloads needed for this contract.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `P79BattlefieldHeldScorePromptQuotesTemporaryPaymentResource`.
- Added `P79BattlefieldHeldScorePromptQuotesTypedTemporaryPaymentResource`.
- Strengthened `P79BattlefieldHeldScorePromptQuotesRecycleAndTemporaryPaymentResourcesTogether`.
- Strengthened accepted generic, typed-red and mixed battlefield-held temporary-resource payment tests in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Payment coverage now asserts prompt metadata, temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, generic and typed temporary-resource accounting, payment id correlation, and event ordering.

## Non-Closure

This narrows battlefield-held temporary-resource quote-command-audit parity only. It does not close full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
