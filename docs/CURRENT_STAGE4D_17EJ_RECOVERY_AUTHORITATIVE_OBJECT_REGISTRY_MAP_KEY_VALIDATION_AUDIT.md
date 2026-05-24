# Stage 4D-17EJ Recovery Authoritative Object-Registry Map-Key Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative object-registry map keys before card-object identity, object-location and object-reference checks consume them. The guard rejects blank map keys, keys with surrounding whitespace, and duplicate normalized keys for both `CardObjects` and `ObjectLocations`.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectRegistryMapKeyDrift` covers card-object map-key whitespace / duplicate / blank diagnostics and object-location map-key whitespace / duplicate / blank diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening: passed `746/746`.
- Backend full: passed `6112/6112`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative object-registry map-key validation. Broader command/recovery/random determinism, object-location lifecycle breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
