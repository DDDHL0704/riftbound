# Stage 4D-201I Recovery Continuous Effect Metadata Lists Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201I adds one server-test shard for spectator recovery replay timing continuous-effect static-aura metadata/list value validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListsWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, mutates only metadata/list values, and leaves the spectator continuous effect count equal to authoritative count.

Metadata/list value drift covered:

- `sourceCardNo`
- `layerEngineStatus`
- `sourceOrder`
- `condition`
- `lifecycle`
- `participantObjectIds`
- `sourceDependencyObjectIds`
- `targetDependencyObjectIds`
- `participantDependencyObjectIds`
- `deferredLayerEngineResiduals`

Assertions prove recovery validation emits:

- Keyed authoritative mismatch diagnostics for all changed metadata/list fields for the static-aura effect id.
- Aggregate continuous-effect disagreement diagnostics for source card numbers, layer engine statuses, source orders, conditions, lifecycles, participant object ids, source dependency object ids, target dependency object ids, participant dependency object ids and deferred LayerEngine residuals.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListsWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1533/1533`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1538/1538`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7808/7808`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `370b417e`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 17:07 CST before docs sync.
- Project remains **NOT READY**.
