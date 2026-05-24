# Stage 4D-17CR Recovery Snapshot Player Coverage Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now validates recovered snapshot player-map coverage against the recovered player views in the frame. Each player snapshot must contain entries for every recovered player id, preventing incomplete snapshot-derived seat maps from feeding baseline restoration when no authoritative state is available.

Test change: `RecoveryValidatorRejectsSnapshotPlayerMapsMissingRecoveredPlayers` builds alice and bob recovered views whose snapshots each omit the other recovered player, then proves both missing-player diagnostics are reported.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `122/122`.
- Adjacent recovery/opening: passed `702/702`.
- Backend full: passed `6068/6068`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered snapshot player-map coverage validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
