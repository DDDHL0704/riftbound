# Stage 4D-17CI Recovery Command Event-Span Continuity Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects recovered command streams where a command's `StartedEventSequence` does not exactly equal the previous command's `CompletedEventSequence`. This makes the recovery validator enforce the journal continuity contract produced by `MatchSession`, instead of relying only on later orphan-event or overlapping-owner diagnostics.

Test change: `RecoveryValidatorRejectsRecoveredCommandEventSpanGap` builds a recovered command stream with a gap from previous completed event sequence `1` to the next command's started event sequence `2`, then proves the validator rejects the discontinuity with an explicit diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `113/113`.
- Adjacent recovery/opening: passed `693/693`.
- Backend full: passed `6059/6059`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command event-span continuity validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
