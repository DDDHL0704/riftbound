# Stage 4D-17EZ Recovery Spectator Battlefield List Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator lane `battlefields` list and dictionary payloads against authoritative battlefield state after identity and scalar parity. The guard covers `occupantObjectIds`, `occupantControllerIds`, `unitsBySide`, spectator-visible `standbyObjectIds`, `scoredThisTurnPlayerIds`, and `pendingTaskKinds`.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldListMismatch` keeps lane identity and scalar payloads intact, corrupts only battlefield list/dictionary fields, and proves explicit diagnostics for each drifted collection while preserving hidden standby redaction and scored-this-turn marker coverage.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `175/175`.
- Adjacent recovery/opening/store-smoke: passed `756/756`.
- Backend full: passed `6121/6121`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator battlefield list/dictionary payload parity for the current lane fields only. Standby slot object details, deeper visible zone/object parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
