# Stage 4D-17CU Recovery Snapshot Structural-Field Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now validates recovered player snapshot structural fields before recovery proceeds. `Lanes`, `Stack`, `Timing` and nonblank `TurnState` are required on each recovered player snapshot.

Test change: `RecoveryValidatorRejectsMissingSnapshotStructuralFields` covers missing lanes, stack, timing and turn-state payload fields, asserting all four diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `125/125`.
- Adjacent recovery/opening: passed `705/705`.
- Backend full: passed `6071/6071`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot structural-field validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
