# Stage 4D-17CO Recovery Player Snapshot Row Agreement Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViewAgreement` now rejects recovered player snapshots whose row tick or event sequence differs across players. This ensures the latest loaded player views represent the same snapshot recovery point before replay-point calculation, player-view comparison and restoration.

Test change: `RecoveryValidatorRejectsPlayerSnapshotRowsThatDisagreeAcrossPlayers` builds player views where alice is at snapshot row tick/event `1` and bob is at snapshot row tick/event `2`, then proves the validator reports both cross-player row-agreement diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `119/119`.
- Adjacent recovery/opening: passed `699/699`.
- Backend full: passed `6065/6065`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only cross-player recovered snapshot row agreement validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
