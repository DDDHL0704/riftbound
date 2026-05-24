# Stage 4D-17CZ Recovery Snapshot Timing Player List Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered player snapshot timing player-list ids in `readyPlayerIds`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` and `destroyedUnitOwnerIdsThisTurn` when they do not exist in the same snapshot `Players` map. Missing/null list fields remain valid.

Test change: `RecoveryValidatorRejectsSnapshotTimingPlayerListsOutsidePlayerMap` covers ready, passed-priority, passed-focus and destroyed-unit-owner timing list entries outside the snapshot player map.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `130/130`.
- Adjacent recovery/opening: passed `710/710`.
- Backend full: passed `6076/6076`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing player-list membership validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
