# Stage 4D-17FW Recovery Command Rune Action Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for rune action payload command types when the required source-object field is missing or malformed. The guarded command types are `TAP_RUNE` and `RECYCLE_RUNE`, covering the rune `sourceObjectId` needed to replay or audit the exact rune source selection. This prevents recovered rune action commands from retaining only `{ "cmdType": ... }` while losing the object identity needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsRuneActionCommandRawPayloadShapeDrift` proves cmdType-only tap-rune payloads and malformed recycle-rune source payloads produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsRuneActionCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `197/197`.
- Adjacent recovery/opening/store-smoke: passed `778/778`.
- Backend full: passed `6143/6143`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the rune-action recovered-command raw-payload field-shape slice. Other payload-bearing commands and broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
