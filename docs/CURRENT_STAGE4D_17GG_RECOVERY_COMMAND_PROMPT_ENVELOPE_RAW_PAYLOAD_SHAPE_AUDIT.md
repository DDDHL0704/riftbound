# Stage 4D-17GG Recovery Command Prompt Envelope Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands when optional replay freshness envelope fields `promptId` or `snapshotTick` are malformed. The fields remain optional, but when present `promptId` must be a non-blank string without surrounding-whitespace drift and `snapshotTick` must be a non-negative JSON integer. This prevents recovered prompt-scoped or snapshot-scoped commands from silently losing, normalizing or ignoring replay freshness metadata during deterministic recovery replay/audit.

Test change: `RecoveryValidatorRejectsRawCommandPromptEnvelopeShapeDrift` proves malformed prompt ids and snapshot ticks produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsRawCommandPromptEnvelopeShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `207/207`.
- Adjacent recovery/opening/store-smoke: passed `788/788`.
- Backend full: passed `6153/6153`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the raw command prompt-envelope field-shape slice for present `promptId` / `snapshotTick`. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
