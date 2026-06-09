# Stage 4D-202S Recovery Continuous Effect Static Aura Source Path Missing Field Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-202S adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `sourcePath` missing-field validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourcePathMissingFieldWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, removes only the static-aura `sourcePath` field, and leaves the spectator continuous effect count equal to authoritative count.

Field covered:

- `sourcePath`

Assertions prove recovery validation emits:

- Static-aura source-path required diagnostics.
- Keyed authoritative `sourcePath` mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect source-path disagreement diagnostics while counts still match.

Assertions also prove this same-count missing-field path does not emit:

- Invalid source-path payload-shape diagnostics.
- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourcePathMissingFieldWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1569/1569`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1574/1574`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7844/7844`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `bf2256fb`.
- Initial push attempt was interrupted by an SSH connection close; retry push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 23:31 CST before docs sync.
- Project remains **NOT READY**.
