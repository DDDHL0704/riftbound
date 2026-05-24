# Stage 4D-17EA Recovery Authoritative Scalar Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative final state scalar metadata before deeper player/object/history checks. Room id must be present and whitespace-clean. Authoritative tick must be nonnegative. Turn number must be positive. Status, phase and timing state must be present, whitespace-clean, and inside the known engine value sets. RNG cursor must be nonnegative.

Test change: `RecoveryValidatorRejectsAuthoritativeStateScalarValueDrift` covers room-id whitespace, negative tick, turn number `0`, status whitespace, invalid phase, timing-state whitespace / invalid value, and negative rng cursor.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `157/157`.
- Adjacent recovery/opening: passed `737/737`.
- Backend full: passed `6103/6103`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative scalar value validation. Broader command/recovery/random determinism, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
