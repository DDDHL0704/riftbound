# Stage 4D-17FZ Recovery Command Declare Battle Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for `DECLARE_BATTLE` when required replay payload fields are missing or malformed. The guarded fields are `battlefieldId`, `attackerObjectIds`, and `defenderObjectIds`, covering battlefield selection plus attacker/defender object identity. This prevents recovered battle declarations from retaining only `{ "cmdType": "DECLARE_BATTLE" }` while losing the combat declaration identity needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsDeclareBattleCommandRawPayloadShapeDrift` proves cmdType-only declare-battle payloads and malformed attacker object arrays produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsDeclareBattleCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `200/200`.
- Adjacent recovery/opening/store-smoke: passed `781/781`.
- Backend full: passed `6146/6146`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the declare-battle recovered-command raw-payload field-shape slice. Other payload-bearing commands and broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
