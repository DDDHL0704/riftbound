# Stage 4D-17TH Recovery Timing Continuous Effect Key Set Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TH narrows P1-004 recovery/replay determinism for spectator replay-frame timing `continuousEffects[]` payloads. The slice targets the gap where spectator continuous-effect count mismatch reported only the list count drift and skipped broad authoritative parity, leaving missing and extra effect ids unnamed.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now compares spectator replay-frame `continuousEffects[]` `effectId` keys against authoritative `MatchState.ContinuousEffects` keys before the count-mismatch early return.

The validator now emits explicit diagnostics for:

- spectator effect ids that are not present in authoritative continuous effects
- authoritative effect ids that are missing from the spectator continuous-effect payload

This check runs alongside the existing same-payload shape/value validation, duplicate effect-id validation, sequence validation, and count mismatch diagnostic. The broad index-based authoritative parity checks still remain behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeySetWithCountMismatch`.

The test builds a spectator replay frame from an authoritative continuous-effect list containing `effect-1`, replaces that effect id with forged `effect-extra-a`, and adds forged `effect-extra-b`. Validation now reports both extra forged effect ids, the missing authoritative effect id, and the existing count mismatch diagnostic.

## Validation

- Focused new test: `1/1`
- Focused ContinuousEffect filter: `130/130`
- Focused recovery filter: `636/636`
- Adjacent recovery/opening/store-smoke filter: `1216/1216`
- Backend full: `6581/6581`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
