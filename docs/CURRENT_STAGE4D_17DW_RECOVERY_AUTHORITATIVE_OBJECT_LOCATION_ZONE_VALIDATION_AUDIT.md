# Stage 4D-17DW Recovery Authoritative Object Location Zone Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `ObjectLocations.Zone` values against the known engine-emitted location vocabulary: `MAIN_DECK`, `RUNE_DECK`, `HAND`, `BASE`, `BATTLEFIELD`, `GRAVEYARD`, `BANISHED`, `LEGEND`, `CHAMPION`, and `STACK`.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectLocationUnknownZone` covers an authoritative object location with `UNKNOWN_ZONE` and asserts the explicit unsupported-zone diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `153/153`.
- Adjacent recovery/opening: passed `733/733`.
- Backend full: passed `6099/6099`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative object-location zone value validation. Broader command/recovery/random determinism, deeper object/location coherence, battlefield/battle ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
