# Stage 4D-17EM Recovery Authoritative Pending Metadata Optional Scalar Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates present optional scalar metadata on authoritative pending payment, pending hand-choice and temporary payment resource state before recovery restore / replay comparison consumes it. The guard preserves emitted empty-string semantics for absent optional fields while rejecting null values, blank whitespace-only values and surrounding-whitespace drift for pending payment reasons, pending hand-choice reasons / source objects / effect kinds, and temporary payment resource source objects / ability ids / payment windows.

Test change: existing pending payment, pending hand-choice and temporary payment resource drift tests now cover those optional scalar diagnostics alongside their existing id/window/list/numeric/reference checks.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening: passed `746/746`.
- Backend full: passed `6112/6112`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only pending metadata optional scalar shape validation. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
