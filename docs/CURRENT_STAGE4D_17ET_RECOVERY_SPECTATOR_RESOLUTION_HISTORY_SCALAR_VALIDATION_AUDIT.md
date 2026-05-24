# Stage 4D-17ET Recovery Spectator Resolution-History Scalar Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now compares spectator snapshot timing battlefield/battle resolution `tick`, `kind`, and `reason` values against authoritative final state resolution histories after count parity is confirmed. This prevents a spectator replay frame from preserving the correct resolution count and ids while drifting core scalar payload metadata.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryScalarMismatch` keeps the spectator resolution ids correct but corrupts battlefield and battle resolution `tick`, `kind`, and `reason` values, then proves explicit scalar mismatch diagnostics for both resolution histories.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `169/169`.
- Adjacent recovery/opening/store-smoke: passed `750/750`.
- Backend full: passed `6115/6115`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only spectator resolution-history scalar parity for `tick`, `kind`, and `reason`. Spectator resolution-history participant/source/player/object-list parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
