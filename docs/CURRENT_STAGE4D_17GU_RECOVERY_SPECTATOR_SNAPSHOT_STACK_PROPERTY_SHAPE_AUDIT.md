# Stage 4D-17GU Recovery Spectator Snapshot Stack Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for spectator snapshot `Stack[]` item payloads. Properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before stack item id, controller id, source object id, effect kind, card number, target object ids, damage amount or destination extraction consumes those payloads. This prevents duplicate JSON properties or spectator snapshot stack property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplaySnapshotStackPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in stack item snapshot objects produce explicit recovery diagnostics while preserving existing stack value parity checks.

Validation:

- Focused single test: new snapshot stack property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `222/222`.
- Adjacent recovery/opening/store-smoke: passed `803/803`.
- Backend full: passed `6168/6168`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator snapshot stack item property-name shape slice. Remaining spectator snapshot nested payload property-name breadth outside stack, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
