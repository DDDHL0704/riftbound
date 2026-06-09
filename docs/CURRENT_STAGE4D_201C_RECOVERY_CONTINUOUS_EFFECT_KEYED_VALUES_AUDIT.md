# Stage 4D-201C Recovery Continuous Effect Keyed Values Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-201C adds one server-test shard for spectator recovery replay timing continuous-effect keyed-value validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedValuesWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates keyed values, and leaves the spectator continuous effect count equal to authoritative count.

Keyed value drift covered:

- `scope` changed to `GLOBAL`.
- `layer` changed to `RULE_TEXT`.
- `targetObjectId` changed to `null`.
- `sourceObjectId` changed to `null`.
- `powerDelta` changed to `0`.
- `effectKind` changed to `WRONG_EFFECT_KIND`.
- `sourcePath` changed to `wrong-source-path`.
- `appliedOrder` changed to `91`.
- `deferredLayerEngineResiduals` changed to `wrong-residual`.

Assertions prove recovery validation emits:

- Keyed authoritative mismatch diagnostics for effect id `effect-1`.
- Aggregate continuous-effect disagreement diagnostics for scopes, layers, target objects, source objects, power deltas, effect kinds, source paths, applied orders and deferred LayerEngine residuals.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedValuesWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1527/1527`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1532/1532`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7802/7802`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `3e9f1e98`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 16:05 CST before docs sync.
- Project remains **NOT READY**.
