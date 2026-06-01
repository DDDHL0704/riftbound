# Stage 4D-17RL Recovery Authoritative Trigger Queue Empty Object Registry Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens authoritative-state trigger queue source-object validation when the authoritative object registry is empty.

Current runtime facts:

- Authoritative `TriggerQueueItemState.SourceObjectId` must be a real source object identity.
- View redaction uses `HIDDEN` only in recovered/spectator payloads; authoritative trigger queue state keeps concrete object ids.
- Existing object-reference validation already rejects authoritative trigger queue source ids outside a non-empty object registry.
- An empty authoritative object registry must not suppress the trigger queue source-object membership diagnostic for a concrete source id.

## Runtime Change

`MatchRecoveryValidator` now validates authoritative trigger queue source-object references before the general authoritative object-reference validator returns on an empty object registry.

The new helper keeps the slice narrow:

- it checks authoritative trigger queue source-object references against canonical `CardObjects` / `ObjectLocations` registry keys;
- it preserves the existing non-empty registry behavior for trigger queue object references;
- it skips blank, whitespace-malformed and `HIDDEN` source ids in this object-reference helper because existing scalar and redaction-sentinel validators already own those diagnostics.

Concrete authoritative trigger queue source ids now emit:

```text
authoritative state trigger queue item <triggerId> source object <sourceObjectId> is missing from object registry
```

even when the authoritative object registry is empty.

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueSourceObjectWithEmptyObjectRegistry`

The test creates an authoritative state with seats and a trigger queue item whose `SourceObjectId` is `source-1`, while `CardObjects` and `ObjectLocations` are both empty. Validation now emits the explicit missing-object-registry diagnostic instead of returning early from object-reference validation.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateTriggerQueueSourceObjectWithEmptyObjectRegistry"` (`1/1`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`86/86`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`564/564`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1145/1145`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6510/6510`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
