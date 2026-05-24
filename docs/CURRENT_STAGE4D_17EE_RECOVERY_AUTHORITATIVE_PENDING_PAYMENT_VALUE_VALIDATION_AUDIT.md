# Stage 4D-17EE Recovery Authoritative Pending Payment Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `PendingPayment` scalar/list metadata independently of player and object-reference checks. The guard rejects blank or whitespace-mutated payment ids and payment windows, negative mana/power costs, malformed power-cost trait maps, malformed / duplicate legal payment choices, and malformed / duplicate payment-resource action lists.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePendingPaymentValueDrift` covers pending payment id/window whitespace, negative mana/power costs, malformed power-cost trait entries, duplicate normalized trait entries, legal-choice whitespace / duplicate / blank entries, and null payment-resource action lists.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `161/161`.
- Adjacent recovery/opening: passed `741/741`.
- Backend full: passed `6107/6107`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative pending payment value validation. Broader command/recovery/random determinism, pending hand-choice value validation, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
