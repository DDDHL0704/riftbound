# Stage 4D-17XS Recovery Timing Trigger Queue Jhin Battlefield Endpoint State Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XS closes one server P1-004 recovery/replay determinism slice for retained Jhin movement-resource trigger queue entries. Runtime creates `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` from move-unit endpoints. Coarse endpoints may be `BASE` or `BATTLEFIELD`; precise battlefield endpoints carry `BATTLEFIELD:<battlefieldObjectId>`. This audit records the matching recovery validator guard for precise battlefield endpoint membership in recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- Move-unit runtime can only use a precise battlefield endpoint when the battlefield object id exists in the current `MatchState.BattlefieldStates`.
- `CoreRuleEngine` rejects movement to a missing precise destination battlefield before creating movement results or Jhin movement-resource triggers.
- Retained Jhin movement-resource trigger ids may use coarse `BASE` / `BATTLEFIELD` endpoints or precise `BATTLEFIELD:<battlefieldObjectId>` endpoints.
- A retained trigger id whose precise battlefield endpoint is absent from recovered lanes or authoritative battlefield states is not runtime-reachable.
- Stage 4D-17XQ separately rejects unsupported endpoint domains; Stage 4D-17XR separately rejects future trigger ticks.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueJhinMovementResourceContext` now receives the applicable battlefield-state id set.
  - Added `ValidateJhinMovementPreciseBattlefieldEndpointForRecovery` and `TryReadJhinPreciseBattlefieldEndpointForRecovery`.
  - Recovered snapshot timing validation passes the snapshot lane battlefield-state ids.
  - Spectator replay-frame and authoritative state validation pass authoritative battlefield-state ids.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame precise battlefield endpoint state drift tests.

## Validation

- Focused new Jhin precise battlefield-state context filter: `3/3`
- Focused `TriggerQueue` filter: `209/209`
- Focused recovery filter: `894/894`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1474/1474`
- Backend full: `6839/6839`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
