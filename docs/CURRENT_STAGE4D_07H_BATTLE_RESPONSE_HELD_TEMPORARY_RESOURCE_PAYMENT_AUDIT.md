# Stage 4D-07H Battle Response Held Temporary Resource Payment Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / battle-response battlefield-held temporary-resource payment parity slice. Project remains **NOT READY**.

## Scope

This slice covers battlefield-held score payment when the held-score trigger is deferred through a battle-response window and later resumes after pass priority, stack resolution or next-contest advancement.

The accepted coverage proves the deferred battle-response paths preserve the temporary payment resource context through response priority and stack resolution, then spend and clear the payment-only ledger once when battlefield-held score resolves. The tests now assert spend / clear payloads for payment id, payment window, player, temporary resource id, source object, ability id, consumed power, remaining power, allowed payment kind and payment-only marker.

The `COST_PAID` coverage now asserts the same payment id / window correlation, battlefield source id, `paymentResourceActions`, temporary resource ids, generic temporary power, empty typed temporary power metadata, remaining power and stable event ordering before score gain, including the next-contested-battlefield advancement path.

No runtime behavior change was required because the existing battle-response resume and PaymentEngine paths already preserved and emitted the needed temporary-resource spend / clear / cost payloads.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `NaturalBattleResponsePreservesHeldScoreTemporaryPaymentResourceContextAfterPass`.
- Strengthened `NaturalBattleResponseActivationPreservesHeldScoreTemporaryPaymentResourceContextAfterStackResolution`.
- Strengthened `NaturalBattleResponseActivationHeldScoreTemporaryPaymentAdvancesNextContestedBattlefieldTask`.
- Payment coverage now asserts temporary-resource spend / clear payloads, `COST_PAID` payment-resource mapping, generic temporary-resource accounting, payment id correlation, and event ordering through score gain / next contest.

## Non-Closure

This narrows battle-response battlefield-held temporary-resource parity only. It does not close full battle lifecycle breadth, full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
