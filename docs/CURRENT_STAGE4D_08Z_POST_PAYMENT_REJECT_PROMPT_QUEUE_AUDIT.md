# Stage 4D-08Z Post-Payment Reject Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 post-payment reject prompt / queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens `NaturalBattleResponseActivationPostPaymentRejectKeepsNextContestedBattlefieldBlocked` in `BattleDamageAssignmentLifecycleTests`. It covers the path where Shadow opens an Icevale trigger payment after battle-response priority closes, BF-NEXT follow-up tasks are already queued, and an invalid `PAY_COST` is rejected without mutating the payment window or advancing BF-NEXT into active spell-duel focus.

Runtime behavior was not changed. The existing invalid post-payment reject path already preserved the pending payment and rejected without events; this slice makes the queued-task / payment-prompt boundary explicit.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertPostPaymentRejectPreservesPayCostPromptQueueAudit(...)`.
- The audit proves invalid `PAY_COST` preserves the same pending payment id, window, player, cost, legal choices, reason and payment-resource action ids.
- It proves BF-NEXT `BATTLEFIELD_CONTESTED`, `START_SPELL_DUEL` and `START_BATTLE` tasks remain queued in deterministic order behind the pending payment.
- It proves those queued BF-NEXT tasks do not become active `SPELL_DUEL_TASKS` and do not emit BF-NEXT contest / spell-duel events after the reject.
- It proves P1 remains actionable on a `PAY_COST` prompt with `PAY_COST` / `SURRENDER`.
- It proves P1 `PAY_COST` candidate metadata and prompt view metadata still match the authoritative pending payment.
- It proves P2 remains non-actionable on the same `PAY_COST` prompt with `WAIT` / `SURRENDER` and no `PAY_COST` candidate.

## Non-Closure

This narrows invalid post-payment reject prompt / queue drift risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full cleanup / replacement-duration breadth, full prompt/reconnect breadth, full hidden-info random-zone breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, READY or goal completion.
