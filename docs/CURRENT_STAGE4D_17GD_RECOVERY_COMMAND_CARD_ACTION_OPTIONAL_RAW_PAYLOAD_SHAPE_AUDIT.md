# Stage 4D-17GD Recovery Command Card Action Optional Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw `HIDE_CARD` and `REVEAL_CARD` commands when optional `optionalCosts` payload fields are malformed. The optional field remains optional, but when present it must be an array of non-blank strings without surrounding-whitespace drift. This prevents recovered card actions from silently losing or accepting malformed optional standby/card-action payment selections during deterministic replay/audit.

Test change: `RecoveryValidatorRejectsCardActionOptionalCommandRawPayloadShapeDrift` proves malformed hide and reveal optional-cost payloads produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsCardActionOptionalCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `204/204`.
- Adjacent recovery/opening/store-smoke: passed `785/785`.
- Backend full: passed `6150/6150`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the card-action optional raw-payload `optionalCosts` field-shape slice. Other optional command payloads, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
