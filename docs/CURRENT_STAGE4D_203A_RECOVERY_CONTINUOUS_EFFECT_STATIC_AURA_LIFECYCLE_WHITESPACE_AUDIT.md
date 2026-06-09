# Stage 4D-203A Recovery Continuous Effect Static Aura Lifecycle Whitespace Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203A adds one server-test shard for spectator recovery replay timing continuous-effect static-aura `lifecycle` whitespace-only validation without a continuous-effect count mismatch while aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLifecycleWhitespaceValueWithoutCountMismatch`

The test builds an authoritative battlefield static-aura continuous effect keyed as `STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:battlefield-1:participant-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative state, changes only the static-aura `lifecycle` field to a whitespace-only string, and leaves the spectator continuous effect count equal to authoritative count.

Field covered:

- `lifecycle`

Assertions prove recovery validation emits:

- Lifecycle required diagnostics.
- Static-aura lifecycle required diagnostics.
- Keyed authoritative `lifecycle` mismatch diagnostics for the static-aura effect id.
- Aggregate continuous-effect lifecycle disagreement diagnostics while counts still match.

Assertions also prove this same-count whitespace-only path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLifecycleWhitespaceValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1577/1577`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1582/1582`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7852/7852`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `f7d5b73a`.
- Initial code push attempt hit an SSH connection close; retry push succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 00:56 CST before docs sync.
- Project remains **NOT READY**.
