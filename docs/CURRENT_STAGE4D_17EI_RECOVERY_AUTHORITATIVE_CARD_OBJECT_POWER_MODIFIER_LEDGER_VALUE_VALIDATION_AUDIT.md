# Stage 4D-17EI Recovery Authoritative Card-Object Power-Modifier Ledger Value Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative card-object `UntilEndOfTurnPowerModifiers` ledger metadata independently of card-object scalar/list validation. The guard rejects null ledger lists, null ledger entries, blank or whitespace-mutated effect ids, effect kinds, durations, target object ids and source paths, duplicate normalized effect ids, zero power deltas, negative minimum power values, resulting power below a positive minimum, invalid or duplicate applied orders, and target-object drift from the owning card object.

Test change: `RecoveryValidatorRejectsAuthoritativeStateCardObjectPowerModifierValueDrift` covers ledger id/kind/duration/target/path whitespace, duplicate effect ids, mismatched target object ids, zero power deltas, negative minimum power, resulting-power floor drift, invalid and duplicate applied orders, null ledger entries, and null ledger lists.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `165/165`.
- Adjacent recovery/opening: passed `745/745`.
- Backend full: passed `6111/6111`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative card-object power-modifier ledger value validation. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
