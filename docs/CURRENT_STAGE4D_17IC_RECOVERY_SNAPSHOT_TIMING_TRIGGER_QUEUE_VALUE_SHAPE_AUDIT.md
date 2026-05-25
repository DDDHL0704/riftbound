# Stage 4D-17IC Recovery Snapshot Timing Trigger Queue Value Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

## Runtime Slice

- Tightened `MatchRecoveryValidator.ValidateSnapshotShape` for recovered player-view snapshot `Timing["triggerQueue"][]` item scalar fields.
- New validation covers `triggerId`, `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind`.
- Required string values now reject blanks and surrounding whitespace before trigger-queue readers consume payloads.
- `triggerId` values now reject duplicate normalized ids inside the recovered trigger queue.
- `sourceVisibility` now rejects values outside `VISIBLE` / `HIDDEN`.
- `sourceObjectId` remains optional-empty compatible, while present non-string or surrounding-whitespace values fail explicitly.

## Test Coverage

- Added `RecoveryValidatorRejectsSnapshotTimingTriggerQueueValueDrift`.
- The test covers duplicate normalized trigger ids, whitespace-mutated trigger ids, controller ids, source object ids, source visibility and effect kinds, blank triggered-event kinds, and invalid source-visibility values.

## Validation

- Focused single test: `1/1`.
- Focused `MatchRecoveryTests`: `256/256`.
- Adjacent recovery/opening/store-smoke: `837/837`.
- Backend full: `6202/6202`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Non-Scope

- No matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln` changes.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

## Remaining Risk

- This narrows P1-004 replay/recovery determinism only.
- Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
