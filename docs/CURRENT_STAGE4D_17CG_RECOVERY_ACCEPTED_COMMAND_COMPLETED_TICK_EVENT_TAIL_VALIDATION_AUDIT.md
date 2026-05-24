# Stage 4D-17CG Recovery Accepted-Command Completed Tick Event-Tail Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now validates accepted command completed-tick metadata against the covered recovered event tail. If a command covers one or more events, its `CompletedTick` must match the max recovered event tick in that command span before recovery restore or replay handoff can consume the recovered history.

Test change: `RecoveryValidatorRejectsAcceptedCommandCompletedTickMismatchCoveredEventTail` builds an accepted command completed at tick `3` with a covered event tail at tick `2`, then proves the validator rejects the mismatch with an explicit diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `111/111`.
- Adjacent recovery/opening: passed `691/691`.
- Backend full: passed `6057/6057`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only accepted-command completed-tick / event-tail metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
