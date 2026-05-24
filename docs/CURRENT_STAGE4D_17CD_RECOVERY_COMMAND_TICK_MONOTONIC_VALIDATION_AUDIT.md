# Stage 4D-17CD Recovery Command Tick Monotonic Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now tracks the previous recovered command completed tick in frame order. A later recovered command whose `StartedTick` is before that previous completed tick is rejected before restore or replay handoff can consume a non-monotonic command timeline.

Test change: `RecoveryValidatorRejectsRecoveredCommandsThatStartBeforePreviousCompletedTick` builds an event-span-stable command stream where `intent-backward-tick` starts at tick `3` after the prior command completed at tick `4`, then proves the validator reports the explicit tick-monotonic diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `108/108`.
- Adjacent recovery/opening: passed `688/688`.
- Backend full: passed `6054/6054`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only command tick monotonic metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
