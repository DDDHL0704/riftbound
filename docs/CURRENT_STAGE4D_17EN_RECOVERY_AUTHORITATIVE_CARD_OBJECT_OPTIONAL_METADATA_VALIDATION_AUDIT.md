# Stage 4D-17EN Recovery Authoritative Card-Object Optional Metadata Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates nullable optional card-object metadata before recovery restore / replay comparison consumes it. The guard preserves `null` as the existing absence shape while rejecting blank strings and surrounding-whitespace drift for authoritative card-object `CardNo` and `AttachedToObjectId` present values.

Test change: `RecoveryValidatorRejectsAuthoritativeStateCardObjectValueDrift` now covers card-object card number whitespace drift and attached-object blank drift alongside existing damage, mana-cost, until-end effect and tag diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `166/166`.
- Adjacent recovery/opening: passed `746/746`.
- Backend full: passed `6112/6112`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only card-object optional metadata shape validation. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
