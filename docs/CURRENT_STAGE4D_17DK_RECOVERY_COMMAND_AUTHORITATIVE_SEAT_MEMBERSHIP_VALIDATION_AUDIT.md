# Stage 4D-17DK Recovery Command Authoritative-Seat Membership Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: when a recovery frame has no player views but does include authoritative state, `MatchRecoveryValidator.ValidateCommands` now validates recovered command players against authoritative `Seats`. This complements Stage 4D-17DJ's player-view membership guard and prevents no-player-view recovery frames from accepting command metadata for players outside the authoritative player set.

Test change: `RecoveryValidatorRejectsRecoveredCommandPlayerOutsideAuthoritativeSeats` covers a recovered `charlie` command against authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `141/141`.
- Adjacent recovery/opening: passed `721/721`.
- Backend full: passed `6087/6087`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command player membership against authoritative seats when player views are unavailable. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
