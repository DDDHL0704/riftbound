# Stage 4D-201D Recovery Continuous Effect Power Metadata Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201D adds one server-test shard for spectator recovery replay timing continuous-effect power-modifier metadata validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierMetadataWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates only power-modifier metadata fields, and leaves the spectator continuous effect count equal to authoritative count.

Metadata drift covered:

- `sourceCardNo` changed from `SRC-001` to `SRC-999`.
- `layerEngineStatus` changed from canonical status to invalid `WRONG_STATUS`.

Assertions prove recovery validation emits:

- Invalid layer-engine-status diagnostics.
- Keyed authoritative mismatch diagnostics for source card number and layer engine status for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for source card numbers and layer engine statuses.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierMetadataWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1528/1528`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1533/1533`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7803/7803`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `9e7e06d9`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 16:14 CST before docs sync.
- Project remains **NOT READY**.
