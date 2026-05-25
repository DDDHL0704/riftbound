# Stage 4D-17IB Recovery Snapshot Timing Continuous Effect List Value Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

## Runtime Slice

- Tightened `MatchRecoveryValidator.ValidateSnapshotShape` for recovered player-view snapshot `Timing["continuousEffects"][]` item list-valued fields.
- New validation covers `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds`, `participantDependencyObjectIds` and `deferredLayerEngineResiduals`.
- Blank values, surrounding-whitespace values and duplicate normalized values now fail recovery validation before continuous-effect list readers can consume canonicality-drifted values.

## Test Coverage

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectListValueDrift`.
- The test covers duplicate normalized values, surrounding-whitespace values and blank values across participant, dependency and deferred LayerEngine residual list fields.

## Validation

- Focused single test: `1/1`.
- Focused `MatchRecoveryTests`: `255/255`.
- Adjacent recovery/opening/store-smoke: `836/836`.
- Backend full: `6201/6201`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Non-Scope

- No matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln` changes.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

## Remaining Risk

- This narrows P1-004 replay/recovery determinism only.
- Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
