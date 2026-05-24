# Stage 4D-17FD Recovery Spectator Visible Object List Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot visible player object list payloads against authoritative `CardObjectState`. The guard covers ordered `tags` and `untilEndOfTurnEffects` after player object coverage, face-down redaction, and visible scalar parity checks pass.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectListMismatch` builds a spectator-visible battlefield object with multiple tags and until-end-of-turn effects, mutates both list payloads, and proves explicit diagnostics for tag and effect-list drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `179/179`.
- Adjacent recovery/opening/store-smoke: passed `760/760`.
- Backend full: passed `6125/6125`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator visible object tag/effect list parity only. Object location parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
