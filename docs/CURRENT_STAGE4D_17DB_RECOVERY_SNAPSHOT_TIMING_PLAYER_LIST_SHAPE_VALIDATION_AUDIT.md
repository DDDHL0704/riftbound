# Stage 4D-17DB Recovery Snapshot Timing Player List Shape Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects malformed present values for recovered player snapshot timing lists `readyPlayerIds`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` and `destroyedUnitOwnerIdsThisTurn` when those values are not string-list payloads. Missing/null list fields remain tolerated.

Test change: `RecoveryValidatorRejectsMalformedSnapshotTimingPlayerLists` covers scalar, non-string enumerable, mixed JSON array and JSON object timing list shapes.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `132/132`.
- Adjacent recovery/opening: passed `712/712`.
- Backend full: passed `6078/6078`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing player-list shape validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
