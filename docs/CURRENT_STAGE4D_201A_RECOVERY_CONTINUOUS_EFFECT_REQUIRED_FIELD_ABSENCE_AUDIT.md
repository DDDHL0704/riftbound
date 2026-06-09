# Stage 4D-201A Recovery Continuous Effect Required Field Absence Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201A adds one server-test shard for spectator recovery replay timing continuous-effect required-field absence validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedRequiredFieldAbsenceWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, removes `scope`, `layer`, `duration`, `powerDelta`, `basePower`, `effectivePower` and `sequence`, and leaves the spectator continuous effect count equal to authoritative count.

Assertions prove recovery validation emits:

- Required-field diagnostics for `scope`, `layer`, `duration`, `powerDelta`, `basePower`, `effectivePower` and `sequence`.
- Keyed authoritative mismatch diagnostics for the same fields on effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for scopes, layers, durations, power deltas, base powers, effective powers and sequences.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedRequiredFieldAbsenceWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1525/1525`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1530/1530`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7800/7800`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `c8f142cc`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 15:29 CST before docs sync.
- Project remains **NOT READY**.
