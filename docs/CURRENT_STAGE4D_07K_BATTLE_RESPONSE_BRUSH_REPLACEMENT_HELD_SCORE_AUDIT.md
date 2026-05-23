# Stage 4D-07K Battle Response Brush Replacement Held-Score Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / battle-response Brush replacement held-score payment-source and event-order parity slice. Project remains **NOT READY**.

## Scope

This slice covers battlefield-held score payment when Brush replacement changes the effective battlefield during a battle-response window before the final held-score resolution.

The accepted coverage proves the deferred battle-response context preserves the Brush replacement choice through response pass, stack resolution and next-contest advancement paths. When held-score payment resumes, the replacement event identifies the original Brush battlefield, replacement battlefield, effective battlefield and replacement reason, and the held-score trigger, `COST_PAID` and `SCORE_GAINED` events consistently use the replacement battlefield object as their source.

The new shared audit assertion verifies `BATTLEFIELD_REPLACEMENT_APPLIED`, `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID` and `SCORE_GAINED` payloads for player, battlefield object ids, reason, power cost, score, payment window, empty resource-action metadata, generic power accounting and stable ordering through battle close / next contest.

No runtime behavior change was required because the existing battle-response Brush replacement resume path already preserved the authoritative replacement battlefield and payment source.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `NaturalBattleResponsePreservesBrushReplacementContextAfterPass`.
- Strengthened `NaturalBattleResponseActivationPreservesBrushReplacementContextAfterStackResolution`.
- Strengthened `NaturalBattleResponseActivationBrushReplacementAdvancesNextContestedBattlefieldTask`.
- Added shared Brush replacement held-score audit assertions for replacement source, held-score source, cost payload, ordinary power accounting, score source and event ordering.

## Non-Closure

This narrows battle-response Brush replacement held-score source / payment-order parity only. It does not close full battle lifecycle breadth, full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
