# Stage 4D-07M Battle Response Post-Payment Trigger Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted server test / battle-response post-payment trigger audit slice. Project remains **NOT READY**.

## Scope

This slice covers Icevale Archer's battle-response post-payment trigger window after a battle-response activation resolves and the current battle closes before the next contested battlefield can advance.

The accepted coverage proves the trigger `PAYMENT_WINDOW_OPENED` payload and `PendingPaymentState` preserve payment id, trigger window, player, battlefield, source, target, mana cost, choice ids and encoded payment reason. The pay path now asserts `COST_PAID`, `BATTLEFIELD_TRIGGER_RESOLVED`, `POWER_MODIFIED_UNTIL_END_OF_TURN` and `PAYMENT_WINDOW_CLOSED` payloads, payment id correlation, submitted / legal choices, remaining resource accounting, power-modifier metadata and event order before next-contest advancement. The decline path now asserts `TRIGGER_PAYMENT_DECLINED` and close payloads, absence of cost / trigger / power-modifier side effects and advancement after closure. The invalid payment path now proves rejected `PAY_COST` preserves the pending payment and does not advance the next contested battlefield.

No runtime behavior change was required because the existing post-payment trigger path already separated open, pay, decline, reject and next-contest advancement correctly.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Strengthened `NaturalBattleResponseActivationPostPaymentBlocksNextContestedBattlefieldUntilAccepted`.
- Strengthened `NaturalBattleResponseActivationPostPaymentDeclineAdvancesNextContestedBattlefield`.
- Strengthened `NaturalBattleResponseActivationPostPaymentRejectKeepsNextContestedBattlefieldBlocked`.
- Added shared Icevale post-payment assertions for opened payment payloads, accepted payment audit chain, declined payment audit chain, invalid payment preservation and event ordering through next-contest advancement.

## Non-Closure

This narrows battle-response post-payment trigger audit parity only. It does not close full battle lifecycle breadth, full PaymentEngine / PAY_COST breadth, all resource-skill official breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
