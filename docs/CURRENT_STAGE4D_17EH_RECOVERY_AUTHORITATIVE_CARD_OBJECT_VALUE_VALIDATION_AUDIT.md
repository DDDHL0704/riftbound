# Stage 4D-17EH Recovery Authoritative Card-Object Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative `CardObjects` value metadata independently of identity, owner/controller and object-reference checks. The guard rejects negative card-object damage values, negative mana-cost values, malformed / duplicate card-object until-end effect ids, and malformed / duplicate card-object tags.

Test change: `RecoveryValidatorRejectsAuthoritativeStateCardObjectValueDrift` covers card-object negative damage, negative mana cost, until-end effect whitespace / duplicate / blank entries, and tag whitespace / duplicate / blank entries.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `164/164`.
- Adjacent recovery/opening: passed `744/744`.
- Backend full: passed `6110/6110`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative card-object value validation for damage, mana cost, until-end effect ids and tags. Broader command/recovery/random determinism, card-object power modifier ledger breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
