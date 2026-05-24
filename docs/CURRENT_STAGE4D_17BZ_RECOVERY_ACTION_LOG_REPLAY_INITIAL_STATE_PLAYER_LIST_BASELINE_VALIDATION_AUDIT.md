# Stage 4D-17BZ Recovery Action-Log Replay Initial-State Player List Baseline Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates replay initial-state player-list baselines before action-log replay or recovery restore can continue. `ReadyPlayerIds`, `PassedPriorityPlayerIds`, `PassedFocusPlayerIds`, `MulliganCompletedPlayerIds` and `DestroyedUnitOwnerIdsThisTurn` must all be empty at the seats-derived opening baseline.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerListBaselineMismatches` builds a real recovered ready command frame, corrupts only replay initial-state player-list metadata, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus explicit diagnostics for each list.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `104/104`.
- Adjacent recovery/opening: passed `684/684`.
- Backend full: passed `6050/6050`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state player-list baseline validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
