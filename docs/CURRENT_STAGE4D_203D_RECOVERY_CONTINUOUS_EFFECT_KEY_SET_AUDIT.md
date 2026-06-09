# Stage 4D-203D Recovery Continuous Effect Key Set Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203D adds one server-test shard for spectator recovery replay timing continuous-effect key-set validation without a continuous-effect count mismatch while aggregate id comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeySetWithoutCountMismatch`

The test builds one authoritative continuous effect keyed as `effect-1`. It starts from the redacted spectator replay frame, keeps the spectator continuous-effect count equal to authoritative count, and changes the single spectator `effectId` to `effect-extra`.

Assertions prove recovery validation emits:

- Unexpected spectator effect id diagnostics for `effect-extra`.
- Missing authoritative effect id diagnostics for `effect-1`.
- Aggregate continuous-effect id disagreement diagnostics while counts still match.

Assertions also prove this same-count key-set path does not emit:

- Continuous-effect count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeySetWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1580/1580`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1585/1585`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7855/7855`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `eb66b7a6`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 01:27 CST before docs sync.
- Project remains **NOT READY**.
