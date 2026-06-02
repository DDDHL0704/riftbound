# Stage 4D-17TI Recovery Timing Continuous Effect Keyed Value Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TI narrows P1-004 recovery/replay determinism for spectator replay-frame timing `continuousEffects[]` payloads. The slice targets the gap left after 17TH: count mismatch now names missing and extra `effectId` keys, but same-key authoritative value drift still relied on broad index-based parity that is skipped when counts differ.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now keys authoritative `MatchState.ContinuousEffects` by `effectId` and validates matching spectator replay-frame `continuousEffects[]` payloads before the count-mismatch early return.

The keyed value validation covers:

- `scope`, `layer`, `duration`
- nullable `targetObjectId` and `sourceObjectId`
- `powerDelta`, `basePower`, `effectivePower`, `sequence`
- optional metadata fields such as `effectKind`, `sourceCardNo`, `sourcePath`, `layerEngineStatus`, `condition` and `lifecycle`
- optional LayerEngine scalar/order values
- optional participant/dependency/residual object-id lists

This check runs alongside the 17TH key-set validation, same-payload shape/value validation, duplicate effect-id validation, sequence validation and count mismatch diagnostic. The broad index-based authoritative parity checks still remain behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedValuesWithCountMismatch`.

The test builds a spectator replay frame from an authoritative continuous-effect list containing `effect-1`, keeps that `effectId` stable, mutates same-key fields, and adds a forged `effect-extra` entry to keep the spectator count-mismatch path active. Validation now reports same-key scope, target/source object, power delta, effect kind, source path, applied order and deferred LayerEngine residual diagnostics before the count-mismatch return.

## Validation

- Focused new test: `1/1`
- Focused ContinuousEffect filter: `131/131`
- Focused recovery filter: `637/637`
- Adjacent recovery/opening/store-smoke filter: `1217/1217`
- Backend full: `6582/6582`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
