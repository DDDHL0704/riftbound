# Stage 4D-17FY Recovery Command Movement Equipment Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for movement/equipment payload command types when required replay payload fields are missing or malformed. The guarded command types are `MOVE_UNIT` and `ASSEMBLE_EQUIPMENT`, covering movement source object identity, origin, destination, equipment source object identity and assemble target identity. This prevents recovered movement/equipment commands from retaining only `{ "cmdType": ... }` while losing the object/location/target identity needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsMovementAndEquipmentCommandRawPayloadShapeDrift` proves cmdType-only move-unit payloads and malformed assemble-equipment target payloads produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsMovementAndEquipmentCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `199/199`.
- Adjacent recovery/opening/store-smoke: passed `780/780`.
- Backend full: passed `6145/6145`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the movement/equipment recovered-command raw-payload field-shape slice. Other payload-bearing commands and broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
