# Stage 4D-17DG Recovery Command Identity Normalization Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects surrounding whitespace in recovered command `PlayerId`, `ClientIntentId` and `CommandType`. Duplicate recovered command identity checks use normalized `(PlayerId, ClientIntentId)` values so whitespace variants cannot bypass recovery-frame uniqueness.

Test change: `RecoveryValidatorRejectsRecoveredCommandIdentityAndTypeWithSurroundingWhitespace` covers whitespace-wrapped command player id, client intent id and command type values.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `137/137`.
- Adjacent recovery/opening: passed `717/717`.
- Backend full: passed `6083/6083`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command identity/type surrounding-whitespace validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
