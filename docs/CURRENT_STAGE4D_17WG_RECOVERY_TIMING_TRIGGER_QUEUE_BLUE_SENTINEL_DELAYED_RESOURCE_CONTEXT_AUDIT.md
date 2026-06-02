# Stage 4D-17WG Recovery Timing Trigger Queue Blue Sentinel Delayed Resource Context Audit

Date: 2026-06-03

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17WG tightened `MatchRecoveryValidator` recovery timing validation for Blue Sentinel delayed-resource `triggerQueue[]` entries across recovered snapshots, authoritative state and spectator replay frames.

Entries whose `triggerId` is shaped as `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::turn::source::battlefield` now reject context drift:

- visible/recovered `sourceObjectId` must match the source object id embedded in the trigger id
- effect kind must be `BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_RESOURCE_SKILL_GAIN_GENERIC_POWER`
- triggered event kind must be `BATTLEFIELD_HELD`
- recovered snapshot and spectator replay payloads must keep the source visibility `VISIBLE`

## Runtime Basis

`CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` creates Blue Sentinel delayed-resource trigger queue items with the source object id embedded in the trigger id, controller set to the defending player, effect kind `BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_RESOURCE_SKILL_GAIN_GENERIC_POWER` and triggered event kind `BATTLEFIELD_HELD`.

Recovery payment-action derivation already parses the same trigger id through `TryReadBlueSentinelDelayedTriggerContextForRecovery` and only treats the trigger as payable when the source/effect/event context matches runtime construction. A retained recovery/replay payload carrying a mismatched source object, effect kind or triggered event kind is therefore replay drift.

This slice changes recovery frame and authoritative-state validation only. It does not change command resolution, protocol shape, frontend code, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or `riftbound-dotnet.sln`.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueBlueSentinelDelayedResourceContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueBlueSentinelDelayedResourceContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueBlueSentinelDelayedResourceContextDrift`

## Validation

- Focused new Blue Sentinel delayed-resource context tests: `3/3`
- Focused `TriggerQueue` filter: `95/95`
- Focused recovery filter: `780/780`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1360/1360`
- Backend full conformance: `6725/6725`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Risk

This narrows P1-004 replay/recovery determinism and Blue Sentinel trigger-queue context integrity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
