# Stage 4D-17BT Recovery Action-Log Replay Initial-State Tick Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates that `ReplayInitialState.Tick` is `0` before action-log replay or recovery restore can continue. A mismatched persisted replay initial-state tick now returns an explicit action-log audit error instead of relying on later hash drift or restore behavior.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStateTickMismatches` builds a real recovered ready command frame, corrupts only the replay initial state's tick, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus the explicit tick mismatch diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `98/98`.
- Adjacent recovery/opening: passed `678/678`.
- Backend full: passed `6044/6044`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state tick metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
