# Stage 4D-17CL Recovery Command Tick Continuity Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects recovered command streams where a command's `StartedTick` does not exactly equal the previous command's `CompletedTick`. This makes command timeline gaps explicit before restore / replay handoff, complementing the earlier nondecreasing tick guard and event-span continuity guard.

Test change: `RecoveryValidatorRejectsRecoveredCommandTickGap` builds a recovered command stream where the first command completes at tick `1` and the next command starts at tick `3`, then proves the validator reports the explicit tick-continuity diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `116/116`.
- Adjacent recovery/opening: passed `696/696`.
- Backend full: passed `6062/6062`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command tick continuity validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
