# Stage 4D-17EC Recovery Authoritative Player-Zone Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `PlayerZones` zone-list metadata independently of object-location coherence. The guard rejects null player-zone values, null zone lists, blank object ids, object ids with surrounding whitespace, and duplicate normalized object ids across all player zones. The object-location/player-zone coherence check now reuses the zone index without duplicating these zone-value diagnostics.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePlayerZoneValueDrift` covers whitespace object ids, duplicate normalized object ids, blank object ids, and null zone lists in corrupted authoritative player-zone state.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `159/159`.
- Adjacent recovery/opening: passed `739/739`.
- Backend full: passed `6105/6105`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative player-zone list value validation. Broader command/recovery/random determinism, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
