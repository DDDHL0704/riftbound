# Stage 4D-17GP Recovery Spectator Timing Window Battle Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay timing object property names for `Timing["turnWindow"]`, `Timing["spellDuel"]`, `Timing["battle"]`, and nested `Timing["battle"]["damageAssignment"]`. Properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before window/battle field extraction consumes those payloads. This prevents duplicate JSON properties or spectator timing window/battle property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingWindowBattlePropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in turn-window, spell-duel, battle and nested damage-assignment timing objects produce explicit recovery diagnostics.

Validation:

- Focused single test: new window/battle property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `217/217`.
- Adjacent recovery/opening/store-smoke: passed `798/798`.
- Backend full: passed `6163/6163`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing window/battle property-name shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
