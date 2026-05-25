# Stage 4D-17GL Recovery Spectator Timing List Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay timing item object property names for `Timing["temporaryPaymentResources"][]` and `Timing["battlefieldTasks"][]`. Item properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before temporary-resource or battlefield-task field extraction consumes the payload. `battlefieldTasks[]` items also now require object payloads explicitly before list-field extraction. This prevents duplicate JSON properties or spectator timing property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskPropertyNameDrift` and `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcePropertyNameDrift` prove duplicate, whitespace-mutated and blank property names in both timing item payload types produce explicit recovery diagnostics.

Validation:

- Focused single tests: both new property-name drift tests passed `2/2`.
- Focused recovery: `MatchRecoveryTests` passed `213/213`.
- Adjacent recovery/opening/store-smoke: passed `794/794`.
- Backend full: passed `6159/6159`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing item property-name shape slice for temporary payment resources and battlefield tasks. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
