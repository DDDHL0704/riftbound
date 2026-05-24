# Stage 4D-17CV Recovery Snapshot Turn-State Timing Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now requires `Timing["timingState"]` on recovered player snapshots and rejects drift between that value and the top-level `TurnState`.

Test change: the recovery player-view helper now emits a valid `timingState`, and `RecoveryValidatorRejectsSnapshotTurnStateTimingStateMismatch` covers missing `timingState` and `TurnState` / `timingState` mismatch diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `126/126`.
- Adjacent recovery/opening: passed `706/706`.
- Backend full: passed `6072/6072`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot turn-state / timing-state consistency validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
