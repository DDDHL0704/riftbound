# Stage 4D-17WF Recovery Timing Trigger Queue Triggered Event Kind Audit

Date: 2026-06-03

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17WF tightened `MatchRecoveryValidator` recovery timing validation for `triggerQueue[]` `triggeredByEventKind` values across recovered snapshots, authoritative state and spectator replay frames.

The validator now rejects values outside the known trigger-queue event set:

- `UNIT_PLAYED_TO_BASE`
- `UNIT_DESTROYED`
- `BATTLEFIELD_HELD`
- `UNIT_MOVED_TO_BATTLEFIELD`
- `UNIT_MOVED_TO_BASE`
- `CARD_PLAYED`
- `BATTLE_DECLARED`
- legacy recovery aliases `OBJECT_DESTROYED` and `UNIT_READY`

## Runtime Basis

`TriggerQueueItemState` normalizes its scalar identity values, and current runtime queue creation writes concrete event kinds from play, destruction, battlefield-held, movement and card-play trigger sources. Snapshot and spectator replay output preserves those trigger values, with source/effect redaction handled separately. A retained recovery/replay payload carrying an arbitrary `triggeredByEventKind` is therefore replay drift.

This slice changes recovery frame and authoritative-state validation only. It does not change command resolution, protocol shape, frontend code, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln`.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTriggeredEventKindDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTriggeredEventKindDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindDrift`

## Validation

- Focused new triggered-event-kind tests: `3/3`
- Focused `TriggerQueue` filter: `92/92`
- Focused recovery filter: `777/777`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1357/1357`
- Backend full conformance: `6722/6722`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Risk

This narrows P1-004 replay/recovery determinism and trigger-queue event-kind integrity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
