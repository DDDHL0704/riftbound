# Stage 4D-201Z Recovery Continuous Effect Static Aura Layer Engine Status Value Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201Z adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `layerEngineStatus` invalid-value validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLayerEngineStatusValueWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only the static-aura `layerEngineStatus` field to `UNKNOWN_STATUS`, and leaves the spectator continuous effect count equal to authoritative count.

Invalid-value field covered:

- `layerEngineStatus`

Assertions prove recovery validation emits:

- Invalid layer-engine-status value diagnostics.
- Static-aura foundation-only layer-engine-status diagnostics.
- Keyed authoritative `layerEngineStatus` mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect layer-engine-status disagreement diagnostics while counts still match.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLayerEngineStatusValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1550/1550`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1555/1555`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7825/7825`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `b7c70883`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 20:04 CST before docs sync.
- Project remains **NOT READY**.
