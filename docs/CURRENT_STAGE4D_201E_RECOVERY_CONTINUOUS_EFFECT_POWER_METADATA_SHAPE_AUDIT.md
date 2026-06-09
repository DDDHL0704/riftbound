# Stage 4D-201E Recovery Continuous Effect Power Metadata Shape Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201E adds one server-test shard for spectator recovery replay timing continuous-effect power-modifier metadata payload-shape validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierMetadataShapeWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates only power-modifier metadata fields to invalid payload shapes, and leaves the spectator continuous effect count equal to authoritative count.

Metadata shape drift covered:

- `effectKind` changed from a canonical string to an array payload.
- `sourceCardNo` changed from a canonical string to a number payload.
- `sourcePath` changed from a canonical string to an object payload.
- `layerEngineStatus` changed from a canonical string to a number payload.

Assertions prove recovery validation emits:

- Invalid-shape diagnostics for effect kind, source card number, source path and layer engine status.
- Keyed authoritative mismatch diagnostics for effect kind, source card number, source path and layer engine status for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for effect kinds, source card numbers, source paths and layer engine statuses.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierMetadataShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1529/1529`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1534/1534`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7804/7804`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `149d734f`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 16:25 CST before docs sync.
- Project remains **NOT READY**.
