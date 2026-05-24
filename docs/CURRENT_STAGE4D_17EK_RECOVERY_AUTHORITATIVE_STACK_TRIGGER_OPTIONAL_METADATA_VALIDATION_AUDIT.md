# Stage 4D-17EK Recovery Authoritative Stack/Trigger Optional Metadata Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates optional stack item / trigger queue scalar metadata before recovery restore / replay comparison consumes it. The guard preserves emitted empty-string semantics for absent optional fields while rejecting null values, blank whitespace-only values and surrounding-whitespace drift for stack source object ids, card numbers, destinations and timing contexts, plus trigger source object ids.

Test change: `RecoveryValidatorRejectsAuthoritativeStateStackAndTriggerValueDrift` now covers stack source object, card number, destination and timing-context whitespace/null/blank diagnostics plus trigger source object whitespace/blank diagnostics alongside existing stack/trigger id/effect/target/damage/repeat diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening: passed `746/746`.
- Backend full: passed `6112/6112`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative stack/trigger optional scalar metadata validation. Broader command/recovery/random determinism, full stack lifecycle breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
