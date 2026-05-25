# Stage 4D-17GH Recovery Command Dev-Seed Scenario Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now requires recovered dev-seed scenario commands to retain persisted raw command payloads and rejects malformed or mismatched raw `scenarioId` values. Raw `DEV_SEED_SCENARIO` payloads must carry a non-blank `scenarioId` without surrounding-whitespace drift, and that value must match the recovered `DEV_SEED_SCENARIO:<scenarioId>` command-type suffix. This prevents recovered dev-seed replay from consuming one scenario id from the recovered command type while the journal raw payload advertises a missing or different scenario id.

Test change: `RecoveryValidatorRejectsDevSeedScenarioRawPayloadShapeDrift` proves missing raw payloads, missing scenario ids, whitespace-mutated scenario ids and mismatched raw/recovered scenario ids produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsDevSeedScenarioRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `208/208`.
- Adjacent recovery/opening/store-smoke: passed `789/789`.
- Backend full: passed `6154/6154`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the dev-seed scenario raw payload shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
