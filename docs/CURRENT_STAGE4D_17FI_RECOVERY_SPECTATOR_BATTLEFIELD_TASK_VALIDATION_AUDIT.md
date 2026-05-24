# Stage 4D-17FI Recovery Spectator Battlefield Task Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `battlefieldTasks` against authoritative computed battlefield tasks. The guard covers required list presence, task count, `taskId`, `kind`, `status`, `reason`, `battlefieldObjectId`, `participantControllerIds`, `participantObjectIds`, optional `actingPlayerId`, and `stackItemIds`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskPayloadMismatch` builds a contested battlefield with two unit controllers, mutates the first spectator battlefield task payload, and proves explicit diagnostics for identity, scalar, participant-list, acting-player, and stack-list drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `184/184`.
- Adjacent recovery/opening/store-smoke: passed `765/765`.
- Backend full: passed `6130/6130`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes. Derived task fields such as `spellDuelId` / `battleId` remain outside this slice.

Remaining risk: this closes spectator timing battlefield-task base payload parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
