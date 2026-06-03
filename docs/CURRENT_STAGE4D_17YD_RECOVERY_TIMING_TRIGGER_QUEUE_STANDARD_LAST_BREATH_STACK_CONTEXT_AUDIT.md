# Stage 4D-17YD Recovery Timing Trigger Queue Standard Last-Breath Stack Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YD closes one server P1-004 recovery/replay determinism slice for retained standard last-breath trigger queue entries. Runtime constructs these trigger ids from a real stack item id, source object id and effect kind. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads whose standard last-breath trigger id keeps the source/effect suffix but omits the stack item context.

## Runtime Evidence

- `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs standard last-breath trigger ids as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}`.
- The existing recovery validator already covers the standard last-breath effect/event context and readable source-object suffix for Watchful Sentinel, Unsung Hero, Scouting Warhawk, Sad Poro, Loyal Poro, Honest Broker, Mechanical Trickster, Undercover Agent, Ironclad Vanguard and Muddy Dredger trigger families.
- Runtime cannot produce `TRIGGER--{sourceObjectId}-{effectKind}` because the source/effect suffix is always preceded by a stack item id from the pending stack item. This slice adds the missing stack-context guard without expanding to friendly-destroyed or Viktor trigger ids that have a different `{sourceObjectId}-{destroyedObjectId}-{effectKind}` suffix shape.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Tightened `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext` so, after the readable source object id matches the expected standard last-breath suffix, the trigger id must still contain a non-empty stack item id before that suffix.
  - Preserved existing source-object mismatch diagnostics as primary when the readable source id does not match the trigger id suffix.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame standard last-breath stack-context drift tests using the Watchful Sentinel representative trigger family.

## Validation

- Focused new standard last-breath stack-context filter: `3/3`
- Focused `TriggerQueue` filter: `242/242`
- Focused recovery filter: `927/927`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1507/1507`
- Backend full: `6872/6872`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
