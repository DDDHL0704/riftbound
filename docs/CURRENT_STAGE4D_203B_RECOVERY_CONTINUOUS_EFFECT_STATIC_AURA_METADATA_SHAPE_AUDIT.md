# Stage 4D-203B Recovery Continuous Effect Static Aura Metadata Shape Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203B adds one server-test shard for spectator recovery replay timing continuous-effect static-aura metadata scalar payload-shape validation without a continuous-effect count mismatch while aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraMetadataShapeWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only static-aura `sourceCardNo`, `layerEngineStatus`, `sourceOrder`, `condition` and `lifecycle` to invalid payload shapes, and leaves the spectator continuous effect count equal to authoritative count.

Fields covered:

- `sourceCardNo`
- `layerEngineStatus`
- `sourceOrder`
- `condition`
- `lifecycle`

Assertions prove recovery validation emits:

- Invalid-shape diagnostics for all mutated scalar metadata fields.
- Keyed authoritative mismatch diagnostics for all mutated scalar metadata fields.
- Aggregate source-card-number, layer-engine-status, source-order, condition and lifecycle disagreement diagnostics while counts still match.

Assertions also prove this same-count metadata-shape path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraMetadataShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1578/1578`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1583/1583`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7853/7853`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `a2cefd91`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 01:06 CST before docs sync.
- Project remains **NOT READY**.
