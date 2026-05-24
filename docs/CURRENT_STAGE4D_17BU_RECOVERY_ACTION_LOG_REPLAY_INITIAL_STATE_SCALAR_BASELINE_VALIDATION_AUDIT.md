# Stage 4D-17BU Recovery Action-Log Replay Initial-State Scalar Baseline Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates the remaining seats-derived replay initial-state scalar baseline before action-log replay or recovery restore can continue. `TurnNumber` must be `1`, `Status` must be `SEATING`, `Phase` must be `ROOM`, `TimingState` must be `ROOM`, `Seed` must be `0`, and `RngCursor` must be `0`.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStateScalarBaselineMismatches` builds a real recovered ready command frame, corrupts only those replay initial-state scalar fields, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus explicit diagnostics for each mismatched scalar.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `99/99`.
- Adjacent recovery/opening: passed `679/679`.
- Backend full: passed `6045/6045`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state scalar baseline validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
