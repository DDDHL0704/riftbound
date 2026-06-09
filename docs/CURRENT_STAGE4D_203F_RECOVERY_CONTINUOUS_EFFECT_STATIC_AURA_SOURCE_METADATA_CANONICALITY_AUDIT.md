# Stage 4D-203F Recovery Continuous Effect Static Aura Source Metadata Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203F adds one server-test shard for spectator recovery replay timing continuous-effect static-aura source metadata surrounding-whitespace canonicality without a continuous-effect count mismatch while aggregate source-card-number and layer-engine-status comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceMetadataCanonicalityWithoutCountMismatch`

The test builds one authoritative static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the spectator continuous-effect count equal to authoritative count, and changes only the spectator `sourceCardNo` and `layerEngineStatus` fields to their canonical values with surrounding whitespace.

Assertions prove recovery validation emits:

- Source-card-number and layer-engine-status surrounding-whitespace diagnostics.
- Keyed authoritative source-card-number and layer-engine-status mismatch diagnostics for the static-aura effect id.
- Aggregate same-count continuous-effect source-card-number and layer-engine-status disagreement diagnostics.

Assertions also prove this same-count source metadata canonicality path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceMetadataCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1582/1582`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1587/1587`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7857/7857`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `a6f35cf3`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 01:47 CST before docs sync.
- Project remains **NOT READY**.
