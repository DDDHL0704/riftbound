# Stage 4D-17CY Recovery Snapshot Timing Optional Player Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects nonblank recovered player snapshot timing optional player pointers `priorityPlayerId`, `focusPlayerId` and `winnerPlayerId` that do not exist in the same snapshot `Players` map. Missing/null optional pointers remain valid.

Test change: `RecoveryValidatorRejectsSnapshotTimingOptionalPlayersOutsidePlayerMap` covers optional timing player pointers `charlie`, `diana` and `eve` while the snapshot player map only contains alice/bob.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `129/129`.
- Adjacent recovery/opening: passed `709/709`.
- Backend full: passed `6075/6075`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing optional-player membership validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
