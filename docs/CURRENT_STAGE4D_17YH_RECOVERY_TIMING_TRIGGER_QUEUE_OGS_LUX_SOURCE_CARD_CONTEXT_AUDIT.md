# Stage 4D-17YH Recovery Timing Trigger Queue OGS Lux Source Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YH closes one server P1-004 recovery/replay determinism slice for retained OGS Lux high-cost spell trigger queue entries. Runtime queues these triggers only from a readable OGS Lux unit source that is not face down and not standby. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` selects source objects whose card number is `OGS·006/024`, whose tags include `CardObjectTags.UnitCard`, whose state is not face down and whose tags do not include `CardObjectTags.Standby`.
- Runtime constructs these trigger ids as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`.
- Prior recovery slices already covered OGS Lux effect/event, source-object suffix parity and stack-context presence. This slice adds source object card/unit/visibility-state reachability checks when the relevant object registry is available.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Added the OGS Lux source card number constant for recovery validation.
  - Tightened `ValidateTriggerQueueOgsLuxHighCostSpellContext` so readable OGS Lux source objects must be `OGS·006/024`, unit-card tagged, not face down and not standby in the applicable recovered snapshot or authoritative object registry.
  - Preserved existing OGS Lux trigger id, stack, source suffix, source visibility, effect kind and event kind diagnostics.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame OGS Lux source-card context drift tests.

## Validation

- Focused new OGS Lux source-card context filter: `3/3`
- Focused `TriggerQueue` filter: `254/254`
- Focused recovery filter: `939/939`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1519/1519`
- Backend full: `6884/6884`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
