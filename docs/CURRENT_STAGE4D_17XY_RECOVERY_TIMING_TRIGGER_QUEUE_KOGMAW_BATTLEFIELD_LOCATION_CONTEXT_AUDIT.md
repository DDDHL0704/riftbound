# Stage 4D-17XY Recovery Timing Trigger Queue Kogmaw Battlefield Location Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XY closes one server P1-004 recovery/replay determinism slice for retained Kogmaw last-breath trigger queue entries. Runtime can only encode a Kogmaw last-breath battlefield object id from a real battlefield context before the destroyed Kogmaw source is removed. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads when an object-location index is available.

## Runtime Evidence

- `CoreRuleEngine.ResolveDestroyedSourceBattlefieldObjectId` returns a battlefield object id from source object locations only when the source is on `BATTLEFIELD` and has a non-empty `BattlefieldObjectId`, with a same-battlefield fallback only from battlefield cleanup resolution.
- `CoreRuleEngine.ResolveKogmawLastBreathAoePlayerId` then requires a destroyed Kogmaw unit whose removal destination is `GRAVEYARD`.
- `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` writes the resolved battlefield object id into `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}`.
- Stage 4D-17WI, 17WJ, 17XC through 17XF and 17XU through 17XX already cover Kogmaw marker/effect/event, nested-prefix, source-object, battlefield-object, battlefield-card, battlefield-state, source-card/unit, source visibility-state, source-controller and source graveyard-location context. This slice adds battlefield object location-zone parity.
- Legacy recovered snapshots without battlefield object-location entries remain compatible. Authoritative state and spectator replay-frame validation use authoritative object locations.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueKogmawLastBreathContext` now rejects a Kogmaw trigger id whose encoded battlefield object id is known, battlefield-card tagged and accepted by the current battlefield-state set, but whose object-location zone is not `BATTLEFIELD`.
  - The new guard uses the recovered snapshot object-location index for recovered payloads and authoritative object locations for authoritative/spectator paths.
  - Missing battlefield object, non-battlefield-card and missing battlefield-state diagnostics remain the primary diagnostics for those earlier context failures.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame battlefield-location drift tests.

## Validation

- Focused new Kogmaw battlefield-location context filter: `3/3`
- Focused `TriggerQueue` filter: `227/227`
- Focused recovery filter: `912/912`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1492/1492`
- Backend full: `6857/6857`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Notes

- An initial backend full attempt used non-existent `Riftbound.sln` and failed before build with `MSB1009`; the correct `Riftbound.slnx` backend full passed `6857/6857`.

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
