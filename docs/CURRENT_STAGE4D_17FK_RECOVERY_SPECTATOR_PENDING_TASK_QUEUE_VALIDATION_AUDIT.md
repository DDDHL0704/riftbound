# Stage 4D-17FK Recovery Spectator Pending Task Queue Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `pendingTaskQueue` base payloads against authoritative computed pending task queue state. The guard covers required queue object presence, `hasTasks`, `isBlocking`, `phase`, spectator-visible `activeTaskId`, top-level task count, metadata `taskCount`, and metadata `stateBasedTaskKinds`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMismatch` builds a contested battlefield with blocking pending tasks, mutates the spectator pending-task queue base payload and metadata, and proves explicit diagnostics for queue state, active task, task count, and metadata drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `186/186`.
- Adjacent recovery/opening/store-smoke: passed `767/767`.
- Backend full: passed `6132/6132`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes. Individual `pendingTaskQueue.tasks[]` cleanup-task payload field parity remains outside this slice.

Remaining risk: this closes spectator timing pending-task queue base payload parity only. Pending queue task item detail parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
