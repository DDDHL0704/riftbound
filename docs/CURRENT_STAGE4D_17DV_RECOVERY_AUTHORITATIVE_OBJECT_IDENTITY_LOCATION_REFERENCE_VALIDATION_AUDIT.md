# Stage 4D-17DV Recovery Authoritative Object Identity/Location Reference Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates card object identity and two more object-reference families. `CardObjects` entries reject `CardObjectState.ObjectId` values that do not match the map key. When an authoritative object registry is available, card-object `AttachedToObjectId` references and object-location `BattlefieldObjectId` references must also resolve through that registry.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectIdentityAndLocationReferenceDrift` covers card object map key / object-id drift, a missing attachment target, and a missing battlefield location target.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `152/152`.
- Adjacent recovery/opening: passed `732/732`.
- Backend full: passed `6098/6098`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative card object identity, attachment reference, and battlefield location reference validation. Broader command/recovery/random determinism, full object-id/location coherence, battlefield/battle ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
