# Stage 4D-17DL Recovery Replay-Initial Seat Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchActionLogReplayer.ValidateRecoveryFrameAsync` now validates replay initial state seats before action-log replay restoration. Seat maps reject blank player ids, player ids with surrounding whitespace, blank seats, seats with surrounding whitespace, values outside `P1` / `P2`, and duplicate normalized seats.

Test change: `ActionLogReplayerRejectsReplayInitialStateSeatValueDrift` covers a replay initial state whose seat map contains duplicate `P1` and invalid `P3` values while matching the authoritative state seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `142/142`.
- Adjacent recovery/opening: passed `722/722`.
- Backend full: passed `6088/6088`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only replay initial state seat value / uniqueness validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
