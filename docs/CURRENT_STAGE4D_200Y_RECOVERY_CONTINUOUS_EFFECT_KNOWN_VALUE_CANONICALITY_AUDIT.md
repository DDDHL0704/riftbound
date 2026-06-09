# Stage 4D-200Y Recovery Continuous Effect Known Value Canonicality Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200Y adds one server-test shard for spectator recovery replay timing continuous-effect known-value and canonicality validation when the spectator continuous effect count still matches authoritative state and aggregate same-count comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedKnownValueCanonicalityWithoutCountMismatch`

The test builds an authoritative tracked power-modifier continuous effect for `effect-1` on `target-1` from `source-1`, including source-path and layer-engine metadata. It starts from the redacted spectator replay frame, keeps the single spectator continuous effect keyed to authoritative `effect-1`, mutates known-value fields to invalid or non-canonical values, and leaves the spectator continuous effect count equal to authoritative count.

Mutations covered:

- `scope = UNKNOWN_SCOPE`
- `layer = UNKNOWN_LAYER`
- `duration = UNKNOWN_DURATION`
- whitespace-padded `targetObjectId`, `sourceObjectId`, `effectKind`, `sourceCardNo` and `sourcePath`
- `layerEngineStatus = UNKNOWN_STATUS`

Assertions prove recovery validation emits:

- Known-value invalid diagnostics for scope, layer, duration and layer-engine status.
- Surrounding-whitespace diagnostics for target object id, source object id, effect kind, source card no and source path.
- Keyed authoritative mismatch diagnostics for all mutated fields under effect id `effect-1`.
- Aggregate same-count disagreement diagnostics for scopes, layers, durations, target objects, source objects, effect kinds, source card numbers, source paths and layer-engine statuses.

Assertions also prove this same-count path does not emit a continuous-effect count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedKnownValueCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1523/1523`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1528/1528`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7798/7798`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `c8f94f71`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 15:07 CST before docs sync.
- Project remains **NOT READY**.
