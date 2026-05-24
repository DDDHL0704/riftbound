# Stage 4D-17CH Recovery Accepted-Command No-Event Tick-Advance Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects accepted recovered commands that advance `StartedTick -> CompletedTick` while covering zero recovered events. Accepted no-event commands may still exist as no-op/idempotent history, but they must not move the recovered timeline forward without a journaled event tail.

Test change: `RecoveryValidatorRejectsAcceptedCommandTickAdvanceWithoutEvents` builds an accepted `PASS` command with no covered events and tick span `1->2`, then proves the validator rejects the mismatch with an explicit diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `112/112`.
- Adjacent recovery/opening: passed `692/692`.
- Backend full: passed `6058/6058`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only accepted-command no-event tick-advance metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
