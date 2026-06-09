# Stage 4D-201B Recovery Continuous Effect Required Field Shape Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201B adds one server-test shard for spectator recovery replay timing continuous-effect required-field payload-shape validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedRequiredFieldShapeWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates required fields to invalid payload shapes, and leaves the spectator continuous effect count equal to authoritative count.

Shape drift covered:

- `scope` as an array payload.
- `layer` as an integer payload where a required string is expected.
- `duration` as an object payload.
- `targetObjectId` as an array payload.
- `sourceObjectId` as an object payload.
- `powerDelta` as a string payload.
- `basePower` as a double payload.
- `effectivePower` as an array payload.
- `sequence` as a string payload.

Assertions prove recovery validation emits:

- Required or invalid-shape diagnostics for the mutated required fields.
- Keyed authoritative mismatch diagnostics for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for scopes, layers, durations, target objects, source objects, power deltas, base powers, effective powers and sequences.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedRequiredFieldShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1526/1526`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1531/1531`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7801/7801`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `2c8ac5ec`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 15:46 CST before docs sync.
- Project remains **NOT READY**.
