# Stage 4D-17BS Recovery Action-Log Replay Initial-State Room Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates `ReplayInitialState.RoomId` against the enclosing `MatchRecoveryFrame.RoomId` before action-log replay or recovery restore can continue. A mismatched persisted replay initial-state room now returns an explicit action-log audit error instead of relying on later hash drift or restore behavior.

Test change: `RegistryRejectsRecoveryFrameWhenReplayInitialStateRoomMismatches` builds a real recovered ready command frame, corrupts only the replay initial state's room id, and proves `InMemoryMatchSessionRegistry` rejects the frame with `ErrorCodes.RecoveryInconsistent` plus the explicit room mismatch diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `97/97`.
- Adjacent recovery/opening: passed `677/677`.
- Backend full: passed `6043/6043`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial-state room metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
