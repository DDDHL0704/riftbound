# Stage 4D-04U LayerEngine Source Order Evidence

Date: 2026-05-21

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This slice adds a bounded server-authoritative continuous-effect source-order foundation:

- `ContinuousEffectState` now carries optional `SourceOrder` metadata for public-field source objects.
- Snapshot `timing.continuousEffects[]` emits additive `sourceOrder` when the source is public and not face-down.
- Existing continuous-effect ordering now uses `sourceOrder` after layer / applied-order and before effect id, so same-target / same-layer static aura effects follow authoritative public field order instead of lexical object id order.
- Face-down sources are excluded from source-order metadata.

This is not a full LayerEngine close. It does not implement keyword gain/loss layering, full timestamp / dependency ordering, replacement / prevention / prohibition breadth, full official card coverage, frontend behavior, Chrome smoke, formal E2E, `fullOfficial`, READY, or matrix count changes.

## Validation

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~LayerEngineTimestampDependencyTests"
```

Result: passed 6/6, failed 0, skipped 0.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LayerEngine|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~Ornn|FullyQualifiedName~BattlefieldStatic"
```

Result: passed 57/57, failed 0, skipped 0.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 5242/5242, failed 0, skipped 0.

## Hidden Information

The source-order helper only assigns order to objects that exist in `CardObjects`, are not face-down, and are in a public field zone. The emitted field is numeric metadata and does not expose hidden card identities, private deck order, face-down standby identity, random results, or server hidden metadata.

## Residual Risk

Still open:

- full official LayerEngine;
- keyword gain/loss layering;
- full dependency / timestamp / replacement / prevention / prohibition breadth;
- card matrix fullOfficial;
- frontend final validation;
- Chrome smoke;
- formal 18-step E2E;
- completion audit and READY.

Final conclusion: **NOT READY**.
