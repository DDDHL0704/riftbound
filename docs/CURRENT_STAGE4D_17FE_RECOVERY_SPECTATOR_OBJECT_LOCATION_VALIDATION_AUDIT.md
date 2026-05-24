# Stage 4D-17FE Recovery Spectator Object Location Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot player object `location` payloads against authoritative object locations. The guard mirrors the snapshot emitter's location resolution order: explicit `ObjectLocations` first, then zone-derived fallback. It covers `playerId`, `zone`, and optional `battlefieldObjectId` for expected spectator-visible objects, including redacted face-down objects that still carry location payloads.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationMismatch` builds a spectator-visible battlefield object with an explicit battlefield location, mutates the location player, zone, and battlefield object id, and proves explicit diagnostics for each drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `180/180`.
- Adjacent recovery/opening/store-smoke: passed `761/761`.
- Backend full: passed `6126/6126`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator player object location parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
