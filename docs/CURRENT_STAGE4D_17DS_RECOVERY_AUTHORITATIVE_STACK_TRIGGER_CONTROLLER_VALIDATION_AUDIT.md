# Stage 4D-17DS Recovery Authoritative Stack/Trigger Controller Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative pending stack and trigger queue controller references after object-player validation. `StackItems` and `TriggerQueue` are required lists; null items are rejected; stack item and trigger queue controller player ids are required, reject surrounding whitespace, and must resolve to a player in authoritative `Seats`.

Test change: `RecoveryValidatorRejectsAuthoritativeStateStackAndTriggerControllersOutsideSeats` covers stack item and trigger queue controller player ids outside authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `149/149`.
- Adjacent recovery/opening: passed `729/729`.
- Backend full: passed `6095/6095`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative stack/trigger controller-player validation. Broader command/recovery/random determinism, object-id/location coherence, object-reference validation, battlefield/battle ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
