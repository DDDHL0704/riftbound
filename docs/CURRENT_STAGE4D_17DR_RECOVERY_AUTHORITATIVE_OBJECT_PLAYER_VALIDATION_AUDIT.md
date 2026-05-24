# Stage 4D-17DR Recovery Authoritative Object-Player Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative object-player references after player-map validation. `CardObjects` owner/controller player ids reject blank values, surrounding whitespace, and ids outside authoritative `Seats` when present; `ObjectLocations` player ids are required, reject surrounding whitespace, and must resolve to a seated player.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectPlayersOutsideSeats` covers card-object owner/controller and object-location player ids outside authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `148/148`.
- Adjacent recovery/opening: passed `728/728`.
- Backend full: passed `6094/6094`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative object-player ownership/location player validation. Broader command/recovery/random determinism, object-id/location coherence, battlefield/stack/trigger ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
