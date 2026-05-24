# Stage 4D-17BX Recovery Action-Log Replay Initial-State Player Resource/Zone Baseline Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates the replay initial-state rune-pool and player-zone dictionaries before action-log replay or recovery restore can continue. Rune-pool and zone player keys must match seats, and all opening rune pools and player zones must be empty.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerResourceBaselineMismatches` builds a real recovered ready command frame, corrupts only replay initial-state rune-pool and hand-zone metadata, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus explicit diagnostics for both non-empty baselines.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `102/102`.
- Adjacent recovery/opening: passed `682/682`.
- Backend full: passed `6048/6048`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state player resource/zone baseline validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
