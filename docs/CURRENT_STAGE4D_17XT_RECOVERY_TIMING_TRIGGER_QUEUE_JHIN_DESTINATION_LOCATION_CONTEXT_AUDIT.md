# Stage 4D-17XT Recovery Timing Trigger Queue Jhin Destination Location Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XT closes one server P1-004 recovery/replay determinism slice for retained Jhin movement-resource trigger queue entries. Runtime consumes `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` only while the encoded destination still matches the source object's current location. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- `CoreRuleEngine.ResolveJhinMovementResourceSkill` calls `JhinMovementTriggerDestinationStillMatches` before granting mana and temporary payment power from a retained Jhin movement-resource trigger.
- Coarse destination `BASE` requires the source object location zone to still be `BASE`.
- Coarse destination `BATTLEFIELD` requires the source object location zone to still be `BATTLEFIELD`.
- Precise destination `BATTLEFIELD:<battlefieldObjectId>` requires the source object location zone to still be `BATTLEFIELD` and its `BattlefieldObjectId` to match the encoded battlefield id.
- Stage 4D-17XS separately verifies that precise battlefield ids exist in the current battlefield-state set; this slice verifies the source is still at that destination.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueJhinMovementResourceContext` now receives the applicable object-location index and label.
  - Recovered snapshot timing validation passes `BuildSnapshotObjectLocationIndex(view.Snapshot)` when player object payloads are available.
  - Spectator replay-frame and authoritative state validation pass authoritative `MatchState.ObjectLocations`.
  - Added `ValidateJhinMovementDestinationLocationForRecovery`, which emits destination/source-location diagnostics only when the encoded source object has a readable location entry.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame destination-location drift tests.

## Validation

- Focused new Jhin destination-location context filter: `3/3`
- Focused `TriggerQueue` filter: `212/212`
- Focused recovery filter: `897/897`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1477/1477`
- Backend full: `6842/6842`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
