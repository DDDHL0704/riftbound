# Stage 4D-195V Recovery Continuous Effect Id Shape Audit

Date: 2026-06-08 12:04 CST

Owner: A_MAIN

Main code commit: `0d992197` (`test: cover spectator continuous effect id shape payload`)

Runtime changed: no. This batch added server recovery validation test coverage only.

## Scope

This slice covers spectator replay timing `continuousEffects[0].effectId` non-string/list-array payload-shape validation without relying on a continuous effect count mismatch or unrelated field drift.

The new `MatchRecoveryTests` case mutates the single redacted spectator replay continuous effect so `effectId` is an array while the authoritative state has one continuous effect with effect id `effect-1`.

## Assertions

- Recovery validation emits `spectator replay frame timing continuous effect item effect id is required`.
- Recovery validation emits `spectator replay frame timing continuous effect item effect id effect-1 is required by authoritative state continuous effects`.
- Recovery validation emits `spectator replay frame timing continuous effect ids disagree with authoritative state continuous effect ids`.
- Recovery validation does not emit a `spectator replay frame timing continuous effect count` diagnostic.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedEffectIdShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1390/1390`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1395/1395`.
- Backend full conformance project: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7665/7665`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

## Coordination

No subagent and no new worktree were created. DOC_MATRIX_CURRENT was observed clean on branch `codex/stage4d-matrix-docs-current` at `17bde0c3`. Push after the code commit succeeded via SSH.

Project remains **NOT READY**. Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, final readiness status, broader command/recovery/random determinism and remaining recovery payload breadth are still open.
