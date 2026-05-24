# Stage 4D-17DC Recovery Snapshot Timing Optional Player Shape Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects malformed present values for recovered player snapshot timing optional player pointers `priorityPlayerId`, `focusPlayerId` and `winnerPlayerId` when those values are not nonblank string ids. Missing/null optional pointers remain tolerated.

Test change: `RecoveryValidatorRejectsMalformedSnapshotTimingOptionalPlayers` covers JSON object, blank string and JSON array optional timing player pointer shapes.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `133/133`.
- Adjacent recovery/opening: passed `713/713`.
- Backend full: passed `6079/6079`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing optional-player pointer shape validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
