# Stage 4D-17DE Recovery Snapshot Timing Scalar Value-Set Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered player snapshots with unknown top-level `TurnState`, timing `timingState`, timing `phase` or timing `roomStatus` scalar values. This keeps recovery metadata inside the closed value sets emitted by `MatchState` / `MatchSession.BuildSnapshotForViewer`.

Test change: `RecoveryValidatorRejectsSnapshotTimingUnknownScalarValues` covers invalid turn-state, timing-state, phase and room-status values.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `135/135`.
- Adjacent recovery/opening: passed `715/715`.
- Backend full: passed `6081/6081`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing scalar value-set validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
