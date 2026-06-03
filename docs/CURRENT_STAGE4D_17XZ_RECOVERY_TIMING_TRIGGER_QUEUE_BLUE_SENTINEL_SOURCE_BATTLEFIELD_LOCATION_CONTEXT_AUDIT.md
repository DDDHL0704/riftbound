# Stage 4D-17XZ Recovery Timing Trigger Queue Blue Sentinel Source Battlefield Location Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XZ closes one server P1-004 recovery/replay determinism slice for retained Blue Sentinel delayed-resource trigger queue entries. Runtime can only offer the delayed resource while the Blue Sentinel source unit still holds the exact battlefield encoded into `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurn}::{sourceObjectId}::{battlefieldObjectId}`. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads when an object-location index is available.

## Runtime Evidence

- `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` only queues Blue Sentinel held delayed-resource triggers for defender objects whose object location is `BATTLEFIELD` and whose `BattlefieldObjectId` equals the held battlefield id.
- `CoreRuleEngine.BlueSentinelDelayedTriggerCanPay` consumes retained triggers only when `BlueSentinelDelayedSourceStillHoldsBattlefield` remains true.
- `CoreRuleEngine.BlueSentinelDelayedSourceStillHoldsBattlefield` requires the source object to be a visible, non-standby Blue Sentinel unit controlled by the paying player, to remain in `BATTLEFIELD`, and to have `ObjectLocationState.BattlefieldObjectId` equal the trigger id battlefield object id.
- Earlier Blue Sentinel recovery slices already cover trigger id/effect/event, source card/unit, source controller, source visibility-state, future captured-turn and battlefield-state context. This slice adds source battlefield-location parity.
- Legacy recovered snapshots without source object-location entries remain compatible. Authoritative state and spectator replay-frame validation use authoritative object locations.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueBlueSentinelDelayedResourceContext` now receives object-location indexes for recovered snapshot, authoritative state and spectator replay-frame validation paths.
  - The new source-location guard rejects known Blue Sentinel delayed-resource source objects whose location zone is not `BATTLEFIELD`.
  - When the source is on `BATTLEFIELD`, the guard also rejects a source `BattlefieldObjectId` that differs from the battlefield object id encoded in the trigger id, after preserving the earlier missing-battlefield-state diagnostic as the primary failure for missing encoded battlefields.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame source battlefield-location drift tests.

## Validation

- Focused new Blue Sentinel source battlefield-location context filter: `3/3`
- Focused `TriggerQueue` filter: `230/230`
- Focused recovery filter: `915/915`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1495/1495`
- Backend full: `6860/6860`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
