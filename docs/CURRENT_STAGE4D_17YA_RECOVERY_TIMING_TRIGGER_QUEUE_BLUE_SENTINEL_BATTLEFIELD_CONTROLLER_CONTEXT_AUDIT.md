# Stage 4D-17YA Recovery Timing Trigger Queue Blue Sentinel Battlefield Controller Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YA closes one server P1-004 recovery/replay determinism slice for retained Blue Sentinel delayed-resource trigger queue entries. Runtime can only offer the delayed resource while the encoded battlefield object still belongs to the paying player when that battlefield card object is known. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads when an object controller index is available.

## Runtime Evidence

- `CoreRuleEngine.BlueSentinelDelayedTriggerCanPay` consumes retained Blue Sentinel delayed-resource triggers only when `BlueSentinelDelayedSourceStillHoldsBattlefield` remains true.
- `CoreRuleEngine.BlueSentinelDelayedSourceStillHoldsBattlefield` requires the source object to remain a valid Blue Sentinel unit on the encoded battlefield, and then requires the encoded battlefield card object, when present, to be controlled by or legacy-owned by the paying player.
- Stage 4D-17XH through 17XL and 17XZ already cover Blue Sentinel trigger id/effect/event, source card/unit, source controller, source visibility-state, captured turn, battlefield-state and source battlefield-location context. This slice adds encoded battlefield controller parity.
- Legacy recovered snapshots without encoded battlefield object controller entries remain compatible. Authoritative state and spectator replay-frame validation use authoritative effective object controllers.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueBlueSentinelDelayedResourceContext` now rejects a Blue Sentinel delayed-resource trigger whose encoded battlefield object id resolves to an effective controller different from the trigger controller.
  - The guard runs only after the encoded battlefield is accepted by the current battlefield-state set, preserving the earlier missing-battlefield-state diagnostic as the primary failure for missing encoded battlefields.
  - The existing recovered snapshot and authoritative controller indexes already use controller id, then owner id, then location player id fallback, matching the recovery-side legacy ownership model.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame battlefield controller drift tests.

## Validation

- Focused new Blue Sentinel battlefield controller context filter: `3/3`
- Focused `TriggerQueue` filter: `233/233`
- Focused recovery filter: `918/918`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1498/1498`
- Backend full: `6863/6863`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
