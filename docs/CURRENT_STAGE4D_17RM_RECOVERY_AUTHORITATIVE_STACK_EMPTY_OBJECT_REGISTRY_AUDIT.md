# Stage 4D-17RM Recovery Authoritative Stack Empty Object Registry Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens authoritative-state stack item object-reference validation when the authoritative object registry is empty.

Current runtime facts:

- Authoritative `StackItemState.SourceObjectId` and `TargetObjectIds` are concrete object references when present.
- Existing object-reference validation already rejects authoritative stack source/target ids outside a non-empty object registry.
- An empty authoritative object registry must not suppress stack source/target missing-object diagnostics.
- This follows the Stage 4D-17RL trigger-queue empty-registry slice and keeps the same P1-004 recovery/replay determinism boundary.

## Runtime Change

`MatchRecoveryValidator` now validates authoritative stack item source/target object references before the general authoritative object-reference validator returns on an empty object registry.

The stack object-reference logic is now factored into a helper used by both empty-registry and non-empty-registry paths:

- stack item source object references are checked against canonical `CardObjects` / `ObjectLocations` registry keys;
- stack item target object lists are checked against the same canonical registry keys;
- the existing non-empty registry behavior is preserved.

Concrete authoritative stack item references now emit:

```text
authoritative state stack item <stackItemId> source object <sourceObjectId> is missing from object registry
authoritative state stack item <stackItemId> target object <targetObjectId> is missing from object registry
```

even when the authoritative object registry is empty.

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateStackItemObjectReferencesWithEmptyObjectRegistry`

The test creates an authoritative state with a stack item whose source and target object ids are concrete while `CardObjects` and `ObjectLocations` are empty. Validation now emits both missing-object-registry diagnostics instead of returning early from object-reference validation.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateStackItemObjectReferencesWithEmptyObjectRegistry"` (`1/1`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "StackAndTrigger|TriggerQueue"` (`88/88`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`565/565`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1146/1146`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6511/6511`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
