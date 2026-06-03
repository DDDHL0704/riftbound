# Stage 4D-17YC Recovery Timing Trigger Queue Blue Sentinel Stale Captured Turn Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YC closes one server P1-004 recovery/replay determinism slice for retained Blue Sentinel delayed-resource trigger queue entries. Runtime queues these triggers with the battlefield-held turn number and can only offer the delayed payment resource on the following turn's current-player open main timing. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads that still carry a Blue Sentinel delayed-resource trigger after that following-turn window has already become stale.

## Runtime Evidence

- `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` creates `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurnNumber}::{sourceObjectId}::{battlefieldObjectId}` with `capturedTurnNumber` set to the current `MatchState.TurnNumber` at battlefield-held resolution.
- `CoreRuleEngine.BlueSentinelDelayedTriggerCanPay` and the matching `MatchSession` prompt helpers require `state.TurnNumber == capturedTurnNumber + 1`, `state.Phase == MAIN`, `state.TimingState == NEUTRAL_OPEN` and `state.ActivePlayerId == pendingPayment.PlayerId` before a delayed-resource payment action can be offered or accepted.
- Same-turn retained entries remain legal because they have just been queued and are not yet payable. Previous-turn entries remain legal because they are in the next-turn payment window. A trigger whose captured turn is older than the previous turn is no longer runtime-reachable.
- Stage 4D-17XI already rejects future captured-turn drift. This slice adds stale captured-turn parity while preserving the existing same-turn and previous-turn queue lifetime.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueBlueSentinelDelayedResourceContext` now rejects Blue Sentinel delayed-resource trigger ids whose captured turn number is less than `currentTurnNumber - 1`.
  - The guard only runs when a positive current turn is available and keeps the existing future captured-turn diagnostic as the primary error for captured turns greater than the current turn.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame stale captured-turn drift tests.

## Validation

- Focused new Blue Sentinel stale captured-turn context filter: `3/3`
- Focused `TriggerQueue` filter: `239/239`
- Focused recovery filter: `924/924`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1504/1504`
- Backend full: `6869/6869`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
