# Stage 4D-17DA Recovery Snapshot Timing Player List Duplicate Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects duplicate normalized player ids in recovered player snapshot timing lists `readyPlayerIds`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` and `destroyedUnitOwnerIdsThisTurn`.

Test change: `RecoveryValidatorRejectsSnapshotTimingPlayerListsWithDuplicates` covers duplicate ready, passed-priority, passed-focus and destroyed-unit-owner timing list entries.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `131/131`.
- Adjacent recovery/opening: passed `711/711`.
- Backend full: passed `6077/6077`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing player-list duplicate validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
