# Stage 4D-17FA Recovery Spectator Standby Slot Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator lane `standbySlots` payloads against authoritative battlefield standby state. The guard covers slot count, `slotId`, `battlefieldObjectId`, `sidePlayerId`, `controllerId`, `visible`, `state`, `isFaceDown`, required visible `objectId`, and redacted hidden `objectId`.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotStandbySlotMismatch` builds one hidden face-down standby slot and one visible standby slot, corrupts their payloads, and proves explicit diagnostics for identity, ownership/controller, visibility/state, face-down flag, hidden-object redaction, and visible-object id parity.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `176/176`.
- Adjacent recovery/opening/store-smoke: passed `757/757`.
- Backend full: passed `6122/6122`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator standby slot payload parity only. Deeper visible object metadata parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
