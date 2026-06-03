# Stage 4D-17XP Recovery Timing Trigger Queue Jhin Movement Zone Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XP closes one server P1-004 recovery/replay determinism slice for retained Jhin movement-resource trigger queue entries. Runtime `CoreRuleEngine.BuildJhinMovementResourceTrigger` creates `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` only when `origin` and `destination` differ. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- `CoreRuleEngine.BuildJhinMovementResourceTrigger` returns null when `origin == destination`.
- The same runtime helper also restricts Jhin movement-resource triggers to the Jhin card, visible non-standby unit source state and moving-player control. Those adjacent source/effect/event, source-card, source-controller and source visibility-state contexts were covered by Stage 4D-17WH, 17XM, 17XN and 17XO.
- This slice deliberately does not further restrict precise battlefield destination label formats; it only rejects identical encoded origin/destination values that runtime cannot enqueue.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueJhinMovementResourceContext` now reads both encoded movement endpoints from the trigger id.
  - When `origin` equals `destination`, recovery validation emits `jhin movement resource origin {origin} and destination {destination} must differ`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame same-zone movement context drift tests.

## Validation

- Focused new Jhin same-zone movement context filter: `3/3`
- Focused `TriggerQueue` filter: `200/200`
- Focused recovery filter: `885/885`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1465/1465`
- Backend full: `6830/6830`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
