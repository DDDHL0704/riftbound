# Stage 4D-17WI Recovery Timing Trigger Queue Kogmaw Last Breath Marker Context Audit

Date: 2026-06-03

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17WI tightened `MatchRecoveryValidator` recovery timing validation for Kogmaw last-breath `triggerQueue[]` entries across recovered snapshots, authoritative state and spectator replay frames.

Entries whose `triggerId` carries `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::...` now reject context drift:

- effect kind must be `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`
- triggered event kind must be `UNIT_DESTROYED`

## Runtime Basis

`CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` creates Kogmaw last-breath trigger queue items with effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`, triggered event kind `UNIT_DESTROYED`, and a battlefield marker in the trigger id. `BuildTriggerQueuedEvent` only emits the Kogmaw `battlefieldObjectId` payload when that effect kind and marker parse together.

This slice intentionally does not parse source object id out of the Kogmaw trigger id. The id shape is `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}::BATTLEFIELD::{battlefieldObjectId}`, and both stack item ids and source object ids may contain hyphens. Existing trigger-queue source object membership validation still checks the retained source object id against the relevant object registry.

This slice changes recovery frame and authoritative-state validation only. It does not change command resolution, protocol shape, frontend code, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln`.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueKogmawLastBreathContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueKogmawLastBreathContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathContextDrift`

## Validation

- Focused new Kogmaw last-breath context tests: `3/3`
- Focused `TriggerQueue` filter: `101/101`
- Focused recovery filter: `786/786`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1366/1366`
- Backend full conformance: `6731/6731`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Risk

This narrows P1-004 replay/recovery determinism and Kogmaw trigger-queue marker context integrity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
