# Stage 4D-17GB Recovery Command Declare Battle Optional Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw `DECLARE_BATTLE` commands when optional replay payload fields are malformed. The optional fields remain optional, but when `optionalCosts` or `battlefieldTargetObjectIds` are present they must be arrays of non-blank strings without surrounding-whitespace drift. This prevents recovered battle declarations from silently losing or accepting malformed optional payment and battlefield target selections during deterministic replay/audit.

Test change: `RecoveryValidatorRejectsDeclareBattleOptionalCommandRawPayloadShapeDrift` proves malformed `optionalCosts` entries and malformed `battlefieldTargetObjectIds` payload shapes produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsDeclareBattleOptionalCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `202/202`.
- Adjacent recovery/opening/store-smoke: passed `783/783`.
- Backend full: passed `6148/6148`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the declare-battle optional raw-payload field-shape slice. Other optional command payloads, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
