# Stage 4D-17XR Recovery Timing Trigger Queue Jhin Future Tick Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XR closes one server P1-004 recovery/replay determinism slice for retained Jhin movement-resource trigger queue entries. Runtime creates `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` with a tick value of `state.Tick + 1` during move-unit resolution. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- `CoreRuleEngine.BuildJhinMovementResourceTrigger` emits `JhinMovementResourceTriggerId(state.Tick + 1, sourceObjectId, origin, destination)`.
- The generated trigger is queued into the state produced for that movement tick.
- Retained trigger queue entries may persist into later ticks, so older encoded ticks remain legal.
- A retained Jhin movement-resource trigger id with an encoded tick greater than the current recovered snapshot or authoritative/replay tick is not runtime-reachable.
- Stage 4D-17XQ separately rejects unsupported movement endpoints; this slice only adds future-tick parity.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueJhinMovementResourceContext` now receives the applicable current tick and rejects parsed Jhin movement-resource trigger ticks greater than that tick.
  - Recovered snapshot timing validation passes `RecoveredPlayerView.SnapshotTick`.
  - Spectator replay-frame timing validation passes `authoritativeState.Tick`, which is already required to match the replay frame tick.
  - Authoritative state trigger queue validation passes `MatchState.Tick`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame future trigger tick context drift tests.
  - Adjusted existing Jhin movement-resource drift fixtures so their otherwise-valid trigger ids use same-tick context and do not accidentally exercise the new future-tick guard.

## Validation

- Focused new Jhin future trigger tick context filter: `3/3`
- Focused `TriggerQueue` filter: `206/206`
- Focused recovery filter: `891/891`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1471/1471`
- Backend full: `6836/6836`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
