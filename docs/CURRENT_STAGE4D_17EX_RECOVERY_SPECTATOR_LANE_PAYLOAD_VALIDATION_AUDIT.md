# Stage 4D-17EX Recovery Spectator Lane Payload Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot lane payloads against the authoritative final state. The guard covers `battlefieldCount`, ordered `battlefieldObjectIds` player/object pairs using the same spectator hidden-standby visibility rule, and `battlefields` object identities.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotLanePayloadMismatch` builds an authoritative battlefield lane with a visible battlefield card, visible units, and a hidden face-down standby object. It corrupts only spectator lane payload values and proves explicit diagnostics for count drift, ordered player/object pair drift, and battlefield identity drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `173/173`.
- Adjacent recovery/opening/store-smoke: passed `754/754`.
- Backend full: passed `6119/6119`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes a first spectator lane payload parity slice only. Deeper visible zone/object parity, full battlefield-state scalar/list parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
