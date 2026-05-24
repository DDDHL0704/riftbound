# Stage 4D-17EW Recovery Spectator Player Scalar Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot player scalar payloads against the authoritative final state. The guard covers missing/extra player rows, payload `id` / `name`, `ready`, `handSize`, `score`, `experience`, `cardsPlayedThisTurn`, `deckSubmitted`, and `mulliganCompleted`.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerScalarMismatch` keeps the spectator player key and seat shape intact, corrupts only scalar player payload values, and proves explicit diagnostics for each drifted field.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `172/172`.
- Adjacent recovery/opening/store-smoke: passed `753/753`.
- Backend full: passed `6118/6118`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator player scalar payload parity only. Spectator lane payload parity, deeper visible zone/object parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
