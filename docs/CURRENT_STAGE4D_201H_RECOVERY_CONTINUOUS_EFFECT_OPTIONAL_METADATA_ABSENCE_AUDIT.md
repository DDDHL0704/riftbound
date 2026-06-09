# Stage 4D-201H Recovery Continuous Effect Optional Metadata Absence Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201H adds one server-test shard for spectator recovery replay timing continuous-effect power-modifier optional/foundation metadata absence validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedOptionalFieldAbsenceWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, removes only optional/foundation metadata fields, and leaves the spectator continuous effect count equal to authoritative count.

Removed metadata fields:

- `effectKind`
- `sourceCardNo`
- `sourcePath`
- `layerEngineStatus`
- `requestedPowerDelta`
- `appliedPowerDelta`
- `minimumPower`
- `resultingPower`
- `appliedOrder`

Assertions prove recovery validation emits:

- Foundation scalar required diagnostics for requested power delta, applied power delta, minimum power, resulting power and applied order when the foundation source object id is still present.
- Keyed authoritative mismatch diagnostics for effect kind, source card number, source path, layer engine status, requested power delta, applied power delta, minimum power, resulting power and applied order for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for effect kinds, source card numbers, source paths, layer engine statuses, requested power deltas, applied power deltas, minimum powers, resulting powers and applied orders.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedOptionalFieldAbsenceWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1532/1532`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1537/1537`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7807/7807`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `af58a487`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 16:55 CST before docs sync.
- Project remains **NOT READY**.
