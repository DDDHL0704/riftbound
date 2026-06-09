# Stage 4D-201G Recovery Continuous Effect Layer Engine Numeric Metadata Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201G adds one server-test shard for spectator recovery replay timing continuous-effect power-modifier numeric metadata value validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedLayerEngineNumericMetadataWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates only numeric power-modifier metadata values, and leaves the spectator continuous effect count equal to authoritative count.

Numeric metadata value drift covered:

- `requestedPowerDelta` changed from `4` to `99`.
- `appliedPowerDelta` changed from `2` to `98`.
- `minimumPower` changed from `1` to `0`.
- `resultingPower` changed from `5` to `6`.

Assertions prove recovery validation emits:

- Keyed authoritative mismatch diagnostics for requested power delta, applied power delta, minimum power and resulting power for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for requested power deltas, applied power deltas, minimum powers and resulting powers.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedLayerEngineNumericMetadataWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1531/1531`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1536/1536`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7806/7806`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `cbbc5135`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 16:45 CST before docs sync.
- Project remains **NOT READY**.
