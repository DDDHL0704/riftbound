# Stage 4D-17BY Recovery Action-Log Replay Initial-State Player Counter Baseline Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates replay initial-state player counters before action-log replay or recovery restore can continue. Score and experience maps must match seated players and all values must be zero. Cards-played-this-turn entries may omit zero counters, matching snapshot normalization, but any present entry must belong to a seated player and must be zero.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerCounterBaselineMismatches` builds a real recovered ready command frame, corrupts only replay initial-state score, experience and cards-played-this-turn metadata, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus explicit diagnostics for all three counter baselines.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `103/103`.
- Adjacent recovery/opening: passed `683/683`.
- Backend full: passed `6049/6049`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state player counter baseline validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
