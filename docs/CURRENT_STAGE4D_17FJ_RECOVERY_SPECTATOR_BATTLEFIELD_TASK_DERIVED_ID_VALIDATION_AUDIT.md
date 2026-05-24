# Stage 4D-17FJ Recovery Spectator Battlefield Task Derived Id Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `battlefieldTasks` derived lifecycle ids against authoritative battlefield task kind and battlefield object id. The guard covers optional `spellDuelId` for `START_SPELL_DUEL` tasks and optional `battleId` for `START_BATTLE` tasks.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskDerivedIdMismatch` builds a contested battlefield with both generated task kinds, mutates spectator `spellDuelId` and `battleId`, and proves explicit diagnostics for each derived id.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `185/185`.
- Adjacent recovery/opening/store-smoke: passed `766/766`.
- Backend full: passed `6131/6131`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing battlefield-task derived id parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
