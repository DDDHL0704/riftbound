# Stage 4D-17EG Recovery Authoritative Until-End Effect Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative top-level `UntilEndOfTurnEffects` list metadata before recovery restore / replay comparison consumes it. The guard rejects null lists, blank effect ids, effect ids with surrounding whitespace, and duplicate normalized effect ids.

Test change: `RecoveryValidatorRejectsAuthoritativeStateUntilEndOfTurnEffectValueDrift` covers until-end effect whitespace, duplicate normalized ids, blank entries and null-list diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `163/163`.
- Adjacent recovery/opening: passed `743/743`.
- Backend full: passed `6109/6109`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative top-level until-end effect value validation. Broader command/recovery/random determinism, card-object until-end/effect ledger breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
