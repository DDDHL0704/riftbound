# Stage 4D-17DX Recovery Authoritative Object Location / Player Zone Coherence Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now builds a deterministic object-location index from authoritative `PlayerZones` and validates non-stack authoritative `ObjectLocations` against it when zone membership is available. The validator rejects duplicate object membership across player zones, object-location entries missing from player zones, and player/zone drift between the two authoritative location views.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectLocationPlayerZoneDrift` covers duplicate `dup-1` membership across alice hand and bob battlefield, `missing-zone-1` present in `ObjectLocations` but absent from `PlayerZones`, and `obj-1` whose `ObjectLocations` entry says `alice/GRAVEYARD` while `PlayerZones` says `alice/HAND`.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `154/154`.
- Adjacent recovery/opening: passed `734/734`.
- Backend full: passed `6100/6100`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative object-location / player-zone coherence validation. Broader command/recovery/random determinism, battlefield/battle ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
