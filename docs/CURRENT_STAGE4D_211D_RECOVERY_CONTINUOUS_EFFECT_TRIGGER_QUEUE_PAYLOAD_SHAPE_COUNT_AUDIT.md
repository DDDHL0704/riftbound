# Stage 4D-211D Recovery Continuous Effect Trigger Queue Payload Shape Count Audit

Date: 2026-06-12 14:05 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `30960ac1`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectAndTriggerQueuePayloadShapeDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing `continuousEffects[]` and `triggerQueue[]` item payload-shape validation when both spectator lists also have count mismatches against authoritative state.

- Authoritative state contains two PowerModifier continuous effects and two trigger queue items.
- The spectator frame rewrites both existing continuous-effect item payloads to non-object values.
- The spectator frame rewrites both existing trigger queue item payloads to non-object values.
- The spectator frame appends one additional malformed continuous-effect item and one additional malformed trigger queue item, so both spectator counts are `3` while authoritative state has `2`.

The validator now has explicit regression coverage proving it reports three continuous-effect item payload-required diagnostics, three trigger queue item payload-required diagnostics, the continuous-effect count mismatch, and the trigger queue count mismatch in the same spectator replay frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectAndTriggerQueuePayloadShapeDriftWithCountMismatch"` -> `1/1`
- Focused continuous-effect: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ContinuousEffect"` -> `293/293`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1788/1788`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1793/1793`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8063/8063`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing continuous-effect and trigger-queue item payload-shape validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
