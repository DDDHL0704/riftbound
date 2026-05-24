# Stage 4D-17DN Recovery Authoritative Player Pointer Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative state active / turn player pointers after seat-map validation. `ActivePlayerId` and `TurnPlayerId` are required, reject surrounding whitespace, and must resolve to a player in authoritative `Seats`.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePlayerPointersOutsideSeats` covers an authoritative state whose active player is `charlie` and turn player is `diana` while seats contain only alice/bob.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `144/144`.
- Adjacent recovery/opening: passed `724/724`.
- Backend full: passed `6090/6090`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative state active / turn player pointer validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
