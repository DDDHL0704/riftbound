# Stage 4D-17YE Recovery Timing Trigger Queue Friendly-Destroyed Stack Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YE closes one server P1-004 recovery/replay determinism slice for retained friendly-destroyed and Viktor destroyed-non-minion trigger queue entries. Runtime constructs these trigger ids from a real stack item id, source object id, destroyed object id and effect kind. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads whose trigger id keeps the readable source/destroyed/effect tail but omits the stack item context.

## Runtime Evidence

- `CoreRuleEngine.BuildGhostlyCentaurFriendlyDestroyedTriggerQueueItems`, `BuildResonantSoulFirstFriendlyDestroyedTriggerQueueItems`, `BuildSavageJawfishFriendlyDestroyedTriggerQueueItems` and `BuildViktorDestroyedNonMinionTriggerQueueItems` construct trigger ids as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}`.
- The existing recovery validator already covers effect-kind, triggered-event and source visibility context for the friendly-destroyed and Viktor destroyed-non-minion trigger families.
- Runtime cannot produce `TRIGGER--{sourceObjectId}-{destroyedObjectId}-{effectKind}` because the source/destroyed/effect tail is always preceded by a stack item id. This slice adds the missing empty-stack guard without trying to fully parse the hyphen-ambiguous destroyed-object segment.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Added `ValidateTriggerQueueFriendlyDestroyedStackContext` for Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion trigger families.
  - Called the helper from recovered snapshot timing payload validation, spectator replay-frame timing payload validation and authoritative state trigger queue validation.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame stack-context drift tests using Ghostly Centaur as the representative friendly-destroyed trigger family.

## Validation

- Focused new friendly-destroyed stack-context filter: `3/3`
- Focused `TriggerQueue` filter: `245/245`
- Focused recovery filter: `930/930`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1510/1510`
- Backend full: `6875/6875`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
