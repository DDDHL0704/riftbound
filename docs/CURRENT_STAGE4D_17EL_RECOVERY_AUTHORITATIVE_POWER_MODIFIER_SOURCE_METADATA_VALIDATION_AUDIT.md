# Stage 4D-17EL Recovery Authoritative Power-Modifier Source Metadata Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates nullable source metadata on authoritative card-object `UntilEndOfTurnPowerModifiers` ledger entries before recovery restore / replay comparison consumes it. The guard accepts `null` as the existing absence shape for nullable ledger source metadata, while rejecting blank strings and surrounding-whitespace drift for `SourceObjectId` and `SourceCardNo`.

Test change: `RecoveryValidatorRejectsAuthoritativeStateCardObjectPowerModifierValueDrift` now covers power-modifier source object and source card number whitespace / blank diagnostics alongside the existing ledger id/kind/duration/target/path/numeric/order diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening: passed `746/746`.
- Backend full: passed `6112/6112`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only nullable source metadata shape validation for authoritative card-object power-modifier ledger entries. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
