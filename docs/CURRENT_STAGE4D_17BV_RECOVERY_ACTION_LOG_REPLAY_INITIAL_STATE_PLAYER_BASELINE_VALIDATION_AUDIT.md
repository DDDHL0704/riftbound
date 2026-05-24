# Stage 4D-17BV Recovery Action-Log Replay Initial-State Player Baseline Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates the seats-derived replay initial-state player baseline before action-log replay or recovery restore can continue. `ActivePlayerId` and `TurnPlayerId` must match the first player by opening seat order (`P1`, `P2`, then stable player id order).

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStatePlayerBaselineMismatches` builds a real recovered ready command frame, corrupts only replay initial-state active/turn player metadata, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus explicit diagnostics for both mismatched players.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `100/100`.
- Adjacent recovery/opening: passed `680/680`.
- Backend full: passed `6046/6046`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state player baseline validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
