# Stage 4D-04R LayerEngine Timestamp Dependency Evidence

Date: 2026-05-21

Conclusion: **VALIDATED / PROJECT NOT READY**

## Scope

This evidence records A-side validation for the 4D-04R-B LayerEngine timestamp / dependency representative slice.

Changed runtime / test files:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

No frontend, matrix JSON, official catalog, Chrome/browser script, formal E2E script, fullOfficial, READY or `riftbound-dotnet.sln` changes were made.

## Validation Commands

Focused 04R tests:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LayerEngineTimestampDependencyTests"
```

Result: **passed 5/5**.

LayerEngine focused / adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Result: **passed 42/42**.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **passed 5231/5231**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## Evidence Summary

The new focused tests verify:

- stable server `sequence` metadata across repeated snapshot generation;
- sequence ordering for direct power modifier, minimum-power floor and static aura representatives;
- static aura source / target / participant dependency metadata;
- hidden face-down equipment not appearing in dependency metadata;
- source leaving public field removes static aura metadata from state and snapshot;
- participant leaving public field recomputes Ornn dependency metadata;
- battlefield participant leaving battlefield recomputes target and participant dependency metadata.

## Non-Closure

This slice does not close full official LayerEngine, P1-001, P1-002, card matrix fullOfficial, frontend final validation, Chrome smoke, formal 18-step E2E, completion audit or READY.
