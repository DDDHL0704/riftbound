# Stage 4D-17GI Recovery Command Raw Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now validates raw command JSON property names before reading `cmdType` or optional replay envelope fields. Raw command properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected. This prevents duplicate JSON properties or property-name drift from being silently resolved by `JsonElement.TryGetProperty` before recovered command replay / audit comparison consumes the persisted raw command payload.

Test change: `RecoveryValidatorRejectsRawCommandPropertyNameDrift` proves duplicate `cmdType`, whitespace-mutated `promptId` property names and blank raw command property names produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsRawCommandPropertyNameDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `209/209`.
- Adjacent recovery/opening/store-smoke: passed `790/790`.
- Backend full: passed `6155/6155`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the raw command property-name shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
