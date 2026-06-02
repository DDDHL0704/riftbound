# Stage 4D-17WH Recovery Timing Trigger Queue Jhin Movement Resource Context Audit

Date: 2026-06-03

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17WH tightened `MatchRecoveryValidator` recovery timing validation for Jhin movement-resource `triggerQueue[]` entries across recovered snapshots, authoritative state and spectator replay frames.

Entries whose `triggerId` is shaped as `JHIN_MOVE_RESOURCE::tick::source::origin::destination` now reject context drift:

- visible/recovered `sourceObjectId` must match the source object id embedded in the trigger id
- effect kind must be `JHIN_MOVEMENT_RESOURCE_SKILL_GAIN_1_MANA_1_POWER`
- triggered event kind must match the trigger-id destination: `UNIT_MOVED_TO_BATTLEFIELD` for battlefield destinations, otherwise `UNIT_MOVED_TO_BASE`

## Runtime Basis

`CoreRuleEngine.BuildJhinMovementResourceTrigger` creates Jhin movement-resource trigger queue items with the source object id, origin and destination embedded in the trigger id. It writes effect kind `JHIN_MOVEMENT_RESOURCE_SKILL_GAIN_1_MANA_1_POWER` and derives `triggeredByEventKind` from whether the destination starts with `BATTLEFIELD`.

Prompt and command resolution already parse the same trigger context before allowing the resource skill to resolve. A retained recovery/replay payload carrying a mismatched source object, effect kind or triggered event kind is therefore replay drift.

This slice changes recovery frame and authoritative-state validation only. It does not change command resolution, protocol shape, frontend code, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln`.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueJhinMovementResourceContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueJhinMovementResourceContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceContextDrift`

## Validation

- Focused new Jhin movement-resource context tests: `3/3`
- Focused `TriggerQueue` filter: `98/98`
- Focused recovery filter: `783/783`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1363/1363`
- Backend full conformance: `6728/6728`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Risk

This narrows P1-004 replay/recovery determinism and Jhin trigger-queue context integrity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
