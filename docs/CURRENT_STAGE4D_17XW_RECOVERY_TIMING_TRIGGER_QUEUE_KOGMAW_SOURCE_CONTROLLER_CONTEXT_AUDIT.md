# Stage 4D-17XW Recovery Timing Trigger Queue Kogmaw Source Controller Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XW closes one server P1-004 recovery/replay determinism slice for retained Kogmaw last-breath trigger queue entries. Runtime creates `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` trigger queue items with a controller resolved from the destroyed Kogmaw source object. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- `CoreRuleEngine.ResolveKogmawLastBreathAoePlayerId` returns `destroyedState.ControllerId ?? destroyedState.OwnerId ?? removalResult.OwnerPlayerId` after verifying the destroyed object is a visible, non-face-down, non-standby Kogmaw unit destroyed to graveyard with a battlefield object id.
- `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` writes that resolved controller id into the queued trigger's `ControllerId`.
- Stage 4D-17WI, 17WJ, 17XC through 17XF, 17XU and 17XV already cover Kogmaw marker/effect/event, nested-prefix, source-object, battlefield-object, battlefield-card, battlefield-state, source-card/unit and source visibility-state context. This slice adds source-controller parity.
- The validator still does not parse the source id from the trigger id because the trigger prefix combines hyphenated stack item ids and hyphenated source object ids.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueKogmawLastBreathContext` now receives trigger `controllerId`, object-controller indexes and labels.
  - Recovered snapshot timing validation passes snapshot object-controller indexes.
  - Authoritative state trigger queue validation passes authoritative object-controller indexes.
  - Spectator replay-frame timing validation passes spectator trigger controller ids plus authoritative object-controller indexes.
  - The guard emits Kogmaw source-controller diagnostics when the source object id and trigger controller are readable and the applicable object-controller index exposes the source controller.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame source-controller drift tests.

## Validation

- Focused new Kogmaw source-controller context filter: `3/3`
- Focused `TriggerQueue` filter: `221/221`
- Focused recovery filter: `906/906`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1486/1486`
- Backend full: `6851/6851`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
