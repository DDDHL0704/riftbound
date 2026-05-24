# Stage 4D-17ES Recovery Spectator Resolution-History ID Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now compares spectator snapshot timing battlefield/battle resolution ids against authoritative final state resolution history after count parity is confirmed. This prevents a spectator replay frame from preserving the right number of resolution records while substituting different resolution identities.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryIdMismatch` corrupts both spectator timing resolution-history ids and proves explicit battlefield-resolution and battle-resolution id diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `168/168`.
- Adjacent recovery/opening/store-smoke: passed `749/749`.
- Backend full: passed `6114/6114`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only spectator resolution-history id parity. Field-by-field spectator resolution-history payload parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
