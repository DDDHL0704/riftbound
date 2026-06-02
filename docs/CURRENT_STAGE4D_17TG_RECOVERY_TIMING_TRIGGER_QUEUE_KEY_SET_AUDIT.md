# Stage 4D-17TG Recovery Timing Trigger Queue Key Set Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TG narrows P1-004 recovery/replay determinism for spectator replay-frame timing `triggerQueue[]` payloads. The slice targets the gap where spectator trigger-queue count mismatch reported only the list count drift and skipped broad authoritative parity, leaving missing and extra trigger ids unnamed.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now compares spectator replay-frame `triggerQueue[]` `triggerId` keys against authoritative `MatchState.TriggerQueue` keys before the count-mismatch early return.

The validator now emits explicit diagnostics for:

- spectator trigger ids that are not present in authoritative trigger queue
- authoritative trigger ids that are missing from the spectator trigger queue payload

This check runs alongside the existing same-payload shape/value validation, count mismatch diagnostic, and Stage 4D-17TF same-key authoritative value validation. Duplicate trigger ids remain covered by the existing duplicate-id validation.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeySetWithCountMismatch`.

The test builds a spectator replay frame from an authoritative queue containing `trigger-visible` and `trigger-hidden`, removes `trigger-hidden`, and adds forged `trigger-extra-a` / `trigger-extra-b` items. Validation now reports both extra forged trigger ids, the missing authoritative trigger id, and the existing count mismatch diagnostic.

## Validation

- Focused new test: `1/1`
- Focused TriggerQueue filter: `88/88`
- Focused recovery filter: `635/635`
- Adjacent recovery/opening/store-smoke filter: `1215/1215`
- Backend full: `6580/6580`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
