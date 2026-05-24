# Stage 4D-17FF Recovery Spectator Player Zone Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot player `zones` payloads against authoritative `PlayerZones`. The guard covers `mainDeckCount`, `runeDeckCount`, redacted empty `hand`, `handHidden`, `base`, spectator-visible `battlefields`, `battlefieldHiddenStandbyCount`, `graveyard`, `banished`, `legendZone`, and `championZone`, including hidden battlefield standby filtering.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerZoneMismatch` builds a spectator snapshot with private hand/deck zones, public zones, and one hidden face-down battlefield standby object, mutates every covered zone field, and proves explicit diagnostics for redaction, count, visible-list, and hidden-standby drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `181/181`.
- Adjacent recovery/opening/store-smoke: passed `762/762`.
- Backend full: passed `6127/6127`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator player zone payload parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
