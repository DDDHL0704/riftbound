# Stage 4D-17CJ Recovery Accepted-Command Covered-Event Completed-Tick Parity Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects accepted command spans whose covered recovered events do not each carry the command `CompletedTick`. This matches `PostgresMatchJournal` persisted `event_tick = entry.CompletedTick` for every event in a command and closes the gap where only the event tick tail was checked.

Test change: `RecoveryValidatorRejectsAcceptedCommandRecoveredEventTickBeforeCompletedTick` builds an accepted command with two events whose tail is at the completed tick but whose first event has an earlier tick, then proves the validator rejects the per-event tick drift with an explicit diagnostic. The valid contiguous recovered-event baseline now uses the journal-shaped completed tick for both covered events.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `114/114`.
- Adjacent recovery/opening: passed `694/694`.
- Backend full: passed `6060/6060`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only accepted-command covered-event completed-tick parity validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
