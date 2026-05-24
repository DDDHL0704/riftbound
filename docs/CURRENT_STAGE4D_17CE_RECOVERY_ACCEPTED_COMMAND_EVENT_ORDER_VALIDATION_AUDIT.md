# Stage 4D-17CE Recovery Accepted-Command Event Order Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now validates accepted command event-order metadata. Events covered by an accepted recovered command span must have `Order` values equal to their 0-based index within that command span, matching the journal persistence contract where `PostgresMatchJournal` writes `event_order = i`.

Test change: `RecoveryValidatorRejectsAcceptedCommandRecoveredEventOrderMismatch` builds an accepted command covering event sequence `1` with recovered order `1`, then proves the validator rejects it because the command expects order `0`. The simple `RecoveredEvent` helper now emits realistic 0-based order for contiguous synthetic events.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `109/109`.
- Adjacent recovery/opening: passed `689/689`.
- Backend full: passed `6055/6055`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only accepted-command event-order metadata validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
