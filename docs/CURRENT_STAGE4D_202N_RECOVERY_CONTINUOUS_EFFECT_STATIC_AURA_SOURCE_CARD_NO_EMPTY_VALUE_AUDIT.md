# Stage 4D-202N Recovery Continuous Effect Static Aura Source Card No Empty Value Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-202N adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `sourceCardNo` empty-value validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoEmptyValueWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only the static-aura `sourceCardNo` field from `OGN·294/298` to an empty string, and leaves the spectator continuous effect count equal to authoritative count.

Field covered:

- `sourceCardNo`

Assertions prove recovery validation emits:

- Source-card-number required diagnostics.
- Static-aura source-card-number required diagnostics.
- Keyed authoritative `sourceCardNo` mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect source-card-number disagreement diagnostics while counts still match.

Assertions also prove this same-count empty-value path does not emit:

- Invalid source-card-number payload-shape diagnostics.
- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoEmptyValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1564/1564`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1569/1569`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7839/7839`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `c5999fa0`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 22:32 CST before docs sync.
- Project remains **NOT READY**.
