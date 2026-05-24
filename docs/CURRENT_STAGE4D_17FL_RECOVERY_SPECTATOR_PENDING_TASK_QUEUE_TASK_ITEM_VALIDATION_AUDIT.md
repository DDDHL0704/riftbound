# Stage 4D-17FL Recovery Spectator Pending Task Queue Task Item Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates each spectator snapshot timing `pendingTaskQueue.tasks[]` item against authoritative computed pending task queue tasks after queue base parity. The guard covers spectator-visible task id, kind, reason, player id, battlefield object id, visible object id, hidden standby object-id redaction, `hiddenObject`, and `hiddenObjectKind`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskItemMismatch` builds a recovery frame with both a hidden illegal standby cleanup task and a public unattached-equipment cleanup task, mutates task item fields, leaks the hidden standby object id, and proves explicit diagnostics for task identity, scalar fields, object id parity, and hidden-object redaction drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `187/187`.
- Adjacent recovery/opening/store-smoke: passed `768/768`.
- Backend full: passed `6133/6133`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing pending-task queue task item parity only. Pending payment, pending hand choice, temporary payment resources, continuous effects, trigger queue, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
