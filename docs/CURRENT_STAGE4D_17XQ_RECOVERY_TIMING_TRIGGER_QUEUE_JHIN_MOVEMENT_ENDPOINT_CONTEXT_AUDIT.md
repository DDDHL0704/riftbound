# Stage 4D-17XQ Recovery Timing Trigger Queue Jhin Movement Endpoint Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XQ closes one server P1-004 recovery/replay determinism slice for retained Jhin movement-resource trigger queue entries. Runtime creates `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` from normalized move-unit endpoints only. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- Normal move-unit resolution passes `BASE` / `BATTLEFIELD` endpoints into `BuildJhinMovementResourceTrigger`.
- Base-to-precise battlefield and precise battlefield movement pass `BATTLEFIELD:<battlefieldObjectId>` precise endpoints.
- Unsupported movement zones such as `HAND`, `GRAVEYARD` or `BASE:<id>` are not runtime-generated Jhin movement-resource endpoints.
- Stage 4D-17XP separately rejects identical origin/destination pairs; this slice only adds endpoint-domain parity.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueJhinMovementResourceContext` now rejects encoded origin/destination values outside `BASE`, `BATTLEFIELD` or `BATTLEFIELD:<battlefieldObjectId>`.
  - Added `IsJhinMovementEndpointForRecovery` to keep the endpoint-domain check scoped to Jhin movement-resource trigger ids.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame movement-endpoint context drift tests.

## Validation

- Focused new Jhin movement-endpoint context filter: `3/3`
- Focused `TriggerQueue` filter: `203/203`
- Focused recovery filter: `888/888`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1468/1468`
- Backend full: `6833/6833`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
