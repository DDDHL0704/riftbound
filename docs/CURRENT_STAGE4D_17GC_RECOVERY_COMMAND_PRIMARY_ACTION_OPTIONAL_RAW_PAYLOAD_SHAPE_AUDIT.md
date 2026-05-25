# Stage 4D-17GC Recovery Command Primary Action Optional Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw `PLAY_CARD`, `ACTIVATE_ABILITY` and `LEGEND_ACT` commands when optional `optionalCosts` payload fields are malformed. The optional field remains optional, but when present it must be an array of non-blank strings without surrounding-whitespace drift. This prevents recovered primary actions from silently losing or accepting malformed optional payment/resource selections during deterministic replay/audit.

Test change: `RecoveryValidatorRejectsPrimaryActionOptionalCommandRawPayloadShapeDrift` proves malformed play, activate and legend optional-cost payloads produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsPrimaryActionOptionalCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `203/203`.
- Adjacent recovery/opening/store-smoke: passed `784/784`.
- Backend full: passed `6149/6149`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the primary-action optional raw-payload `optionalCosts` field-shape slice. Other optional command payloads, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
