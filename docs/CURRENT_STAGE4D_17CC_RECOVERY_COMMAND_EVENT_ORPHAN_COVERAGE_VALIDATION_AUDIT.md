# Stage 4D-17CC Recovery Command/Event Orphan Coverage Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now validates recovered event ownership whenever a recovered command stream is present. Every recovered event sequence must be covered by an accepted recovered command span; otherwise the recovery frame is rejected before restore or replay handoff can treat orphan events as valid recovered history. Commandless spectator replay validation remains allowed.

Test change: `RecoveryValidatorRejectsRecoveredEventNotCoveredByAcceptedCommand` builds a contiguous event stream with two events and one accepted command that only covers sequence `1`, then proves the validator rejects orphan event sequence `2` with an explicit diagnostic.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `107/107`.
- Adjacent recovery/opening: passed `687/687`.
- Backend full: passed `6053/6053`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered event orphan coverage. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
