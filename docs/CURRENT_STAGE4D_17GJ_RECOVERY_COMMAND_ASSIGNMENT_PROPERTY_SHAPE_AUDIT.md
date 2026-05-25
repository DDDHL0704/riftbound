# Stage 4D-17GJ Recovery Command Assignment Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now reuses raw command object property-name validation for nested `ASSIGN_COMBAT_DAMAGE` assignment objects. Assignment object properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before `sourceObjectId`, `targetObjectId` or `damage` lookups consume the nested payload. This prevents duplicate JSON properties or assignment property-name drift from being silently resolved by `JsonElement.TryGetProperty` during recovered combat-damage command replay / audit validation.

Test change: `RecoveryValidatorRejectsCombatAssignmentPropertyNameDrift` proves duplicate `sourceObjectId`, whitespace-mutated `targetObjectId` property names and blank assignment property names produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsCombatAssignmentPropertyNameDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `210/210`.
- Adjacent recovery/opening/store-smoke: passed `791/791`.
- Backend full: passed `6156/6156`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the nested combat-assignment raw property-name shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
