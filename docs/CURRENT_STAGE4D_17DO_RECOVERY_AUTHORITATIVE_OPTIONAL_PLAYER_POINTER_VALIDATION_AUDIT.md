# Stage 4D-17DO Recovery Authoritative Optional Player Pointer Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative state optional player pointers after seat-map validation. `PriorityPlayerId`, `FocusPlayerId`, `WinnerPlayerId`, `OpeningSecondActionPlayerId`, and `ExtraTurnPlayerId` reject blank values, reject surrounding whitespace, and must resolve to a player in authoritative `Seats` when present.

Test change: `RecoveryValidatorRejectsAuthoritativeStateOptionalPlayerPointersOutsideSeats` covers priority/focus/winner/opening-second/extra-turn ids outside authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `145/145`.
- Adjacent recovery/opening: passed `725/725`.
- Backend full: passed `6091/6091`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative state optional player pointer validation. Broader command/recovery/random determinism, authoritative player-list validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
