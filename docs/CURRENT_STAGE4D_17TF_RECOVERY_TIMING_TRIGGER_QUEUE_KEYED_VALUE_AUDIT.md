# Stage 4D-17TF Recovery Timing Trigger Queue Keyed Value Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TF narrows P1-004 recovery/replay determinism for spectator replay-frame timing `triggerQueue[]` payloads. The slice targets the gap where spectator trigger-queue count mismatch caused the broad count-equal authoritative parity check to return before reporting same-`triggerId` field drift.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now builds an authoritative `MatchState.TriggerQueue` index keyed by `triggerId` and validates readable same-key spectator replay-frame fields before the count-mismatch early return:

- `controllerId`
- spectator-redacted or visible `sourceObjectId`
- `sourceVisibility`
- spectator-redacted or visible `effectKind`
- `triggeredByEventKind`

Hidden standby sources still use the existing spectator redaction policy: expected `sourceObjectId` and `effectKind` are `HIDDEN`, and expected `sourceVisibility` is `HIDDEN`. Visible sources require the authoritative source id, `VISIBLE`, and the authoritative effect kind.

Missing or extra trigger ids remain represented by the existing trigger-queue count mismatch diagnostics and broad count-equal parity checks when counts match. This slice only adds same-key field diagnostics that were previously skipped under count mismatch.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedValuesWithCountMismatch`.

The new test forges a spectator replay frame with a valid authoritative `triggerId`, but drifts the visible trigger's controller, source redaction, visibility, effect kind and triggered-event kind while adding an extra trigger to force count mismatch. Validation now reports explicit same-key authoritative diagnostics plus the existing count mismatch diagnostic.

## Validation

- Focused new test: `1/1`
- Focused TriggerQueue filter: `87/87`
- Focused recovery filter: `634/634`
- Adjacent recovery/opening/store-smoke filter: `1214/1214`
- Backend full: `6579/6579`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
