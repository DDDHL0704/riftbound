# Stage 4D-17ED Recovery Authoritative Stack/Trigger Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `StackItems` and `TriggerQueue` scalar/list metadata independently of player and object-reference checks. The guard rejects blank or whitespace-mutated stack/trigger ids, duplicate normalized ids, blank or whitespace-mutated stack/trigger effect or event-kind values, malformed stack target / optional-cost lists, negative stack damage, and invalid stack repeat counts.

Test change: `RecoveryValidatorRejectsAuthoritativeStateStackAndTriggerValueDrift` covers stack id whitespace, duplicate stack ids, stack effect-kind whitespace / blank values, target-list whitespace / blank values, null optional-cost lists, negative stack damage, invalid repeat counts, trigger id whitespace, duplicate trigger ids, and trigger effect/event-kind whitespace / blank values.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `160/160`.
- Adjacent recovery/opening: passed `740/740`.
- Backend full: passed `6106/6106`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative stack/trigger value validation. Broader command/recovery/random determinism, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
