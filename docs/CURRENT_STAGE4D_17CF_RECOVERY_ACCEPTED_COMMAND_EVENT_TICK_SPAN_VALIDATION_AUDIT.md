# Stage 4D-17CF Recovery Accepted-Command Event Tick Span Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now validates accepted command event tick bounds. Events covered by an accepted recovered command span must have ticks between that command's `StartedTick` and `CompletedTick`, inclusive, before recovery restore or replay handoff can consume the recovered history.

Test change: `RecoveryValidatorRejectsAcceptedCommandRecoveredEventTickOutsideCommandSpan` builds an accepted command with tick span `1->3` and a covered event at tick `4`, then proves the validator rejects the mismatch with an explicit diagnostic. The valid contiguous recovered-event test now uses a command completed tick that covers both accepted events.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `110/110`.
- Adjacent recovery/opening: passed `690/690`.
- Backend full: passed `6056/6056`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only accepted-command event tick-span metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
