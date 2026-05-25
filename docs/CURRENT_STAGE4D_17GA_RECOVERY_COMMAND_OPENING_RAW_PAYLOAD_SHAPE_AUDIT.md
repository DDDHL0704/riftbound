# Stage 4D-17GA Recovery Command Opening Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for opening payload command types when required replay payload fields are missing or malformed. The guarded command types are `SUBMIT_DECK` and `MULLIGAN`, covering deck identity fields (`legendCardNo`, `championCardNo`, `mainDeck`, `runeDeck`, `battlefields`) and mulligan hand-object selections (`handObjectIds`). This prevents recovered opening commands from retaining only `{ "cmdType": ... }` while losing the decklist or mulligan selection identity needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsOpeningCommandRawPayloadShapeDrift` proves cmdType-only submit-deck and mulligan payloads plus malformed deck / hand arrays produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsOpeningCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `201/201`.
- Adjacent recovery/opening/store-smoke: passed `782/782`.
- Backend full: passed `6147/6147`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the opening-command recovered raw-payload field-shape slice. Other broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
