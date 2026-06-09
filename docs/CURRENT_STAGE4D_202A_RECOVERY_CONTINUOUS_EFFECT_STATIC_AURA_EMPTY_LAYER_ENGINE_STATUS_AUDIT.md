# Stage 4D-202A Recovery Continuous Effect Static Aura Empty Layer Engine Status Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-202A adds one server-test shard for spectator recovery replay timing continuous-effect static-aura empty `layerEngineStatus` validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLayerEngineStatusEmptyValueWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only the static-aura `layerEngineStatus` field to the empty string, and leaves the spectator continuous effect count equal to authoritative count.

Empty-value field covered:

- `layerEngineStatus`

Assertions prove recovery validation emits:

- Required layer-engine-status diagnostics.
- Static-aura foundation-only layer-engine-status diagnostics.
- Keyed authoritative `layerEngineStatus` mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect layer-engine-status disagreement diagnostics while counts still match.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLayerEngineStatusEmptyValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1551/1551`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1556/1556`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7826/7826`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `c4742efa`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 20:12 CST before docs sync.
- Project remains **NOT READY**.
