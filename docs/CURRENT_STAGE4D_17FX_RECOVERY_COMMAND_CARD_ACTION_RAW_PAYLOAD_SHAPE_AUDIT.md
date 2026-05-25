# Stage 4D-17FX Recovery Command Card Action Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for card-action payload command types when required replay payload fields are missing or malformed. The guarded command types are `HIDE_CARD` and `REVEAL_CARD`, covering card source object identity, card number identity and reveal target-object arrays. This prevents recovered card-action commands from retaining only `{ "cmdType": ... }` while losing the card/source/target identity needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsCardActionCommandRawPayloadShapeDrift` proves cmdType-only hide-card payloads and malformed reveal-card target arrays produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsCardActionCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `198/198`.
- Adjacent recovery/opening/store-smoke: passed `779/779`.
- Backend full: passed `6144/6144`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the card-action recovered-command raw-payload field-shape slice. Other payload-bearing commands and broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
