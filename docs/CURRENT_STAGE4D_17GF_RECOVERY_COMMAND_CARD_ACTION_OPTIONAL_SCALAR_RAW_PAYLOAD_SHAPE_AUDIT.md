# Stage 4D-17GF Recovery Command Card Action Optional Scalar Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw `PLAY_CARD`, `HIDE_CARD` and `REVEAL_CARD` commands when optional scalar `mode` or `destination` payload fields are malformed. The fields remain optional, but when present they must be non-blank strings without surrounding-whitespace drift. This prevents recovered card commands from silently losing or accepting malformed card mode / destination selections during deterministic replay/audit.

Test change: `RecoveryValidatorRejectsCardActionOptionalScalarCommandRawPayloadShapeDrift` proves malformed play mode, hide destination, and reveal mode/destination payloads produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsCardActionOptionalScalarCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `206/206`.
- Adjacent recovery/opening/store-smoke: passed `787/787`.
- Backend full: passed `6152/6152`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the card command optional raw-payload scalar field-shape slice for present `mode` / `destination`. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
