# Stage 4D-17ER Recovery Spectator Resolution-History Count Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now requires spectator snapshot timing `battlefieldResolutions` and `battleResolutions` arrays to be present and count-aligned with the authoritative final state. This prevents a spectator replay frame from passing recovery validation while omitting or truncating authoritative resolution history.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryCountMismatch` corrupts both spectator timing resolution-history arrays and proves explicit battlefield-resolution and battle-resolution count diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `167/167`.
- Adjacent recovery/opening/store-smoke: passed `748/748`.
- Backend full: passed `6113/6113`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only spectator resolution-history count parity. Field-by-field spectator resolution-history parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
