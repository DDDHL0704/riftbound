# Stage 4D-07I Battle Response Held Recycle Resource Payment Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / battle-response battlefield-held recycle-rune payment parity slice. Project remains **NOT READY**.

## Scope

This slice covers battlefield-held score payment when the held-score trigger is deferred through a battle-response window and later resumes after pass priority, stack resolution or next-contest advancement.

The accepted coverage proves the deferred battle-response paths preserve the recycle-rune payment-resource context through response priority and stack resolution, then commit the recycle resource action exactly once when battlefield-held score resolves. The tests now assert `RUNE_RECYCLED` and `POWER_GAINED` payloads for payment id, payment window, player, source rune id, card number, recycle ability id, red trait, generated power, rune-deck count and post-gain power totals.

The `COST_PAID` coverage now asserts the same payment id / window correlation, battlefield source id, `paymentResourceActions`, recycled rune ids, empty temporary-resource ids and power metadata, generic power cost, total power cost, remaining power and stable event ordering before score gain, including the next-contested-battlefield advancement path.

No runtime behavior change was required because the existing battle-response resume, recycle-rune payment-resource and PaymentEngine paths already preserved and emitted the needed recycle / power / cost payloads.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `NaturalBattleResponsePreservesHeldScorePaymentResourceContextAfterPass`.
- Strengthened `NaturalBattleResponseActivationPreservesHeldScorePaymentResourceContextAfterStackResolution`.
- Strengthened `NaturalBattleResponseActivationHeldScoreRecyclePaymentAdvancesNextContestedBattlefieldTask`.
- Added shared battle-response recycle-resource audit assertions for recycle / power / `COST_PAID` payloads, payment id correlation, empty temporary-resource metadata, remaining-power accounting and event ordering through score gain / next contest.

## Non-Closure

This narrows battle-response battlefield-held recycle-rune payment parity only. It does not close full battle lifecycle breadth, full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
