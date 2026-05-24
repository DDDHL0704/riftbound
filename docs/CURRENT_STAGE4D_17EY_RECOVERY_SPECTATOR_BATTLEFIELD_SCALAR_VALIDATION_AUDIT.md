# Stage 4D-17EY Recovery Spectator Battlefield Scalar Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator lane `battlefields` scalar payloads against authoritative battlefield state after the 17EX identity check. The guard covers `zonePlayerId`, `cardNo`, `controllerId`, `status`, `contested`, `standbySlotCount`, `faceDownStandbyCount`, and spectator-visible `hiddenStandbyCount`.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldScalarMismatch` keeps the spectator lane identities intact, corrupts only battlefield scalar payload fields, and proves explicit diagnostics for each drifted field while retaining hidden face-down standby coverage.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `174/174`.
- Adjacent recovery/opening/store-smoke: passed `755/755`.
- Backend full: passed `6120/6120`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator battlefield scalar payload parity only. Battlefield list/dictionary payload parity, standby slot object details, units-by-side parity, deeper visible zone/object parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
