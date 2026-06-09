# Stage 4D-203C Recovery Continuous Effect Metadata List Shape Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203C adds one server-test shard for spectator recovery replay timing continuous-effect metadata list payload-shape validation without a continuous-effect count mismatch while aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListShapeWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only metadata list fields to invalid payload shapes, and leaves the spectator continuous effect count equal to authoritative count.

Fields covered:

- `participantObjectIds`
- `sourceDependencyObjectIds`
- `targetDependencyObjectIds`
- `participantDependencyObjectIds`
- `deferredLayerEngineResiduals`

Assertions prove recovery validation emits:

- List-payload-required diagnostics for all mutated metadata list fields.
- Keyed authoritative mismatch diagnostics for all mutated metadata list fields.
- Aggregate participant-object-id, source-dependency-object-id, target-dependency-object-id, participant-dependency-object-id and deferred-LayerEngine-residual disagreement diagnostics while counts still match.

Assertions also prove this same-count metadata-list shape path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1579/1579`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1584/1584`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7854/7854`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `a58c89cc`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 01:17 CST before docs sync.
- Project remains **NOT READY**.
