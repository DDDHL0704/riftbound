# Stage 4D-200X Recovery Continuous Effect Duplicate Id Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200X adds one server-test shard for spectator recovery replay timing continuous-effect duplicate effect-id validation when the spectator continuous effect count still matches authoritative state and keyed authoritative value checks continue to run against the normalized duplicate id.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedDuplicateIdWithoutCountMismatch`

The test builds an authoritative state with two continuous power-modifier effects, `effect-1` and `effect-2`, sourced from `source-1` and applied to `target-1`. It starts from the redacted spectator replay frame, verifies the two spectator effects differ by effect id, sequence, power delta and source card number, changes the second spectator effect id from `effect-2` to duplicate `effect-1`, and leaves the spectator continuous effect count equal to authoritative count.

Assertions prove recovery validation emits:

- Duplicate `effect-1` diagnostic.
- Missing authoritative `effect-2` diagnostic.
- Keyed authoritative power-delta mismatch for `effect-1`.
- Keyed authoritative sequence mismatch for `effect-1`.
- Keyed authoritative source-card-number mismatch for `effect-1`.
- Aggregate continuous-effect id disagreement diagnostic.

Assertions also prove this same-count path does not emit:

- Unexpected `effect-1` not-present diagnostic.
- Continuous effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedDuplicateIdWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1522/1522`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1527/1527`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7797/7797`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `04bab9c7`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 14:54 CST before docs sync.
- Project remains **NOT READY**.
