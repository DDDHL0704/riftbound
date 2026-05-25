# Stage 4D-17FV Recovery Command Primary Action Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for primary action payload command types when the required replay payload fields are missing or malformed. The guarded command types are `PLAY_CARD`, `ACTIVATE_ABILITY` and `LEGEND_ACT`, covering source object ids, card / ability ids and target object id arrays. This prevents recovered primary action commands from retaining only `{ "cmdType": ... }` while losing the source, action identity or target selection needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsPrimaryActionCommandRawPayloadShapeDrift` proves cmdType-only play / legend payloads and malformed activate-ability target arrays produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsPrimaryActionCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `196/196`.
- Adjacent recovery/opening/store-smoke: passed `777/777`.
- Backend full: passed `6142/6142`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the first primary-action recovered-command raw-payload field-shape slice. Other payload-bearing commands and broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
