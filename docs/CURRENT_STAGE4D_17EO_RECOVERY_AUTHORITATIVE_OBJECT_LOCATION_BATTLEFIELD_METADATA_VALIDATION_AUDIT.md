# Stage 4D-17EO Recovery Authoritative Object-Location Battlefield Metadata Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates nullable object-location battlefield metadata before recovery restore / replay comparison consumes it. The guard preserves `null` as the existing absence shape while rejecting blank strings and surrounding-whitespace drift for authoritative `ObjectLocationState.BattlefieldObjectId` present values.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectIdentityAndLocationReferenceDrift` now injects raw corrupted object-location battlefield metadata after `MatchState` construction and covers surrounding-whitespace and blank diagnostics alongside the existing object-registry reference check.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening: passed `746/746`.
- Backend full: passed `6112/6112`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only object-location battlefield metadata shape validation. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
