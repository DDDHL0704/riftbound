# Stage 4D-17EQ Recovery Authoritative Resolution Object-List Metadata Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates battlefield/battle resolution object-list metadata before recovery restore / replay comparison consumes it. The guard rejects blank entries, surrounding-whitespace drift and duplicate normalized entries for battlefield participant objects and battle attacker/defender/survivor/destroyed object lists, while leaving object-registry existence checks in the existing reference-validation stage.

Test change: `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryValueDrift` now injects raw corrupted battlefield participant and battle attacker object-list metadata after `MatchState` construction and covers whitespace, duplicate and blank diagnostics alongside the existing resolution-history value checks.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening/store-smoke: passed `747/747`.
- Backend full: passed `6112/6112`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only battlefield/battle resolution object-list metadata shape validation. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
