# Stage 4D-17EU Recovery Spectator Resolution-History Scalar Reference Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now compares spectator snapshot timing scalar reference fields against authoritative final state resolution histories after count parity is confirmed. The new guard covers battlefield resolution `battlefieldObjectId`, `playerId`, `previousControllerId`, `controllerId`, and `sourceObjectId`, plus battle resolution `battlefieldId`, `attackingPlayerId`, `defendingPlayerId`, and `winnerPlayerId`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryScalarReferenceMismatch` keeps spectator resolution ids and `tick` / `kind` / `reason` values correct, corrupts only scalar reference fields, and proves explicit diagnostics for battlefield and battle resolution histories.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `170/170`.
- Adjacent recovery/opening/store-smoke: passed `751/751`.
- Backend full: passed `6116/6116`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only spectator resolution-history scalar reference parity. Spectator resolution-history participant/object-list and related-event parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
