# Stage 4D-17CK Recovery Command Started-Event-Sequence Upper-Bound Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects recovered commands whose `StartedEventSequence` is after the recovery frame's match last event sequence. This makes the start-bound violation explicit before restore / replay handoff, complementing the existing completed-event upper-bound and completed-before-start diagnostics.

Test change: `RecoveryValidatorRejectsCommandStartedEventSequenceAfterMatchSequence` builds a recovered command with `StartedEventSequence=2` while the match last event sequence is `1`, then proves the validator reports the explicit start-bound diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `115/115`.
- Adjacent recovery/opening: passed `695/695`.
- Backend full: passed `6061/6061`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command started-event upper-bound validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
