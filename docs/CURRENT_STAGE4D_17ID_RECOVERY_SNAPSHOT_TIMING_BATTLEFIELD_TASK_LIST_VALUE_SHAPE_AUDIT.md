# Stage 4D-17ID Recovery Snapshot Timing Battlefield Task List Value Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

## Runtime Slice

- Tightened `MatchRecoveryValidator.ValidateSnapshotShape` for recovered player-view snapshot `Timing["battlefieldTasks"][]` item list-valued fields.
- New validation covers `participantControllerIds`, `participantObjectIds` and `stackItemIds`.
- Present string lists now reject malformed list payloads, blank values, surrounding whitespace and duplicate normalized values before battlefield-task readers consume those payloads.
- Optional/missing list fields remain compatible.

## Test Coverage

- Added `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskListValueDrift`.
- The test covers duplicate normalized participant-controller, participant-object and stack-item ids, whitespace-mutated list values and blank list values.

## Validation

- Focused single test: `1/1`.
- Focused `MatchRecoveryTests`: `257/257`.
- Adjacent recovery/opening/store-smoke: `838/838`.
- Backend full: `6203/6203`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Non-Scope

- No matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln` changes.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

## Remaining Risk

- This narrows P1-004 replay/recovery determinism only.
- Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
