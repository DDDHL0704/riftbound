# Stage 4D-17GK Recovery Spectator Timing Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay timing item object property names for `Timing["continuousEffects"][]` and `Timing["triggerQueue"][]`. Item properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before continuous-effect or trigger-queue field lookups consume the payload. This prevents duplicate JSON properties or spectator timing property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingPayloadPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in both continuous-effect and trigger-queue spectator timing item payloads produce explicit recovery diagnostics.

Validation:

- Focused single test: `RecoveryValidatorRejectsSpectatorReplayTimingPayloadPropertyNameDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `211/211`.
- Adjacent recovery/opening/store-smoke: passed `792/792`.
- Backend full: passed `6157/6157`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing item property-name shape slice for `continuousEffects` and `triggerQueue`. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
