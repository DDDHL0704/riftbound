# Stage 4D-17YF Recovery Timing Trigger Queue Kogmaw Stack Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YF closes one server P1-004 recovery/replay determinism slice for retained Kogmaw last-breath trigger queue entries. Runtime constructs these trigger ids from a real stack item id, source object id, effect kind and encoded battlefield object id. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads whose Kogmaw trigger id keeps the readable source/effect/battlefield marker tail but omits the stack item context.

## Runtime Evidence

- `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` constructs Kogmaw trigger ids as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}`.
- Existing recovery validation already covers Kogmaw effect/event, nested marker boundary, source object, battlefield object, battlefield card/state/location, source card/unit, source visibility, source controller and source graveyard-location context.
- Runtime cannot produce `TRIGGER--{sourceObjectId}-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}` because the source/effect/battlefield tail is always preceded by a stack item id. This slice adds the missing empty-stack guard without expanding broader hyphen-ambiguous stack/source parsing.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Tightened `ValidateTriggerQueueKogmawLastBreathContext` so readable Kogmaw trigger ids reject an empty stack item segment before the source object id.
  - Preserved the existing source-object mismatch diagnostic and all Kogmaw battlefield/source runtime parity checks.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame Kogmaw stack-context drift tests.

## Validation

- Focused new Kogmaw stack-context filter: `3/3`
- Focused `TriggerQueue` filter: `248/248`
- Focused recovery filter: `933/933`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1513/1513`
- Backend full: `6878/6878`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
