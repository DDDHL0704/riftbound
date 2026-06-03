# Stage 4D-17YG Recovery Timing Trigger Queue OGS Lux Stack Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YG closes one server P1-004 recovery/replay determinism slice for retained OGS Lux high-cost spell trigger queue entries. Runtime constructs these trigger ids from a real stack item id, source object id and effect kind. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads whose OGS Lux trigger id keeps the readable source/effect tail but omits the stack item context.

## Runtime Evidence

- `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` constructs OGS Lux high-cost spell trigger ids as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`.
- Existing recovery validation already covers OGS Lux effect kind, triggered event kind and source object id suffix parity.
- Runtime cannot produce `TRIGGER--{sourceObjectId}-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` because the source/effect tail is always preceded by a stack item id. This slice adds the missing empty-stack guard while preserving hyphen-safe suffix validation for stack/source ids.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Tightened `ValidateTriggerQueueOgsLuxHighCostSpellContext` so readable OGS Lux trigger ids reject an empty stack item segment before the source object id.
  - Preserved the existing source-object mismatch diagnostic and OGS Lux effect/event validation.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame OGS Lux stack-context drift tests.

## Validation

- Focused new OGS Lux stack-context filter: `3/3`
- Focused `TriggerQueue` filter: `251/251`
- Focused recovery filter: `936/936`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1516/1516`
- Backend full: `6881/6881`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
