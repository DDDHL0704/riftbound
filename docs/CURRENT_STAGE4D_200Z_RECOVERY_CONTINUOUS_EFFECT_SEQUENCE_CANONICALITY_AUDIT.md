# Stage 4D-200Z Recovery Continuous Effect Sequence Canonicality Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200Z adds one server-test shard for spectator recovery replay timing continuous-effect sequence canonicality validation when the spectator continuous effect count still matches authoritative state and aggregate sequence comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedSequenceCanonicalityWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, changes only `sequence` from `1` to `0`, and leaves the spectator continuous effect count equal to authoritative count.

Assertions prove recovery validation emits:

- `sequence 0 must be positive`.
- Keyed authoritative sequence mismatch for effect id `effect-1`.
- Aggregate continuous-effect sequence disagreement.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedSequenceCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1524/1524`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1529/1529`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7799/7799`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `f0279c9a`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 15:17 CST before docs sync.
- Project remains **NOT READY**.
