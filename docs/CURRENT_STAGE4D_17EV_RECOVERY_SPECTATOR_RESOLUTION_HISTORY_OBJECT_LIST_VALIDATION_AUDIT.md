# Stage 4D-17EV Recovery Spectator Resolution-History Object-List Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now compares spectator snapshot timing object-list and related-event fields against authoritative final state resolution histories after count parity is confirmed. The new guard covers battlefield resolution `participantObjectIds` / `relatedEventKinds`, plus battle resolution attacker, defender, surviving attacker, surviving defender, destroyed object ids, and `relatedEventKinds`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryObjectListMismatch` keeps spectator resolution ids, scalar payload, and scalar reference fields correct, corrupts only list payloads, and proves explicit diagnostics for battlefield and battle resolution histories.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `171/171`.
- Adjacent recovery/opening/store-smoke: passed `752/752`.
- Backend full: passed `6117/6117`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator resolution-history list parity for the currently emitted battlefield/battle resolution histories. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
