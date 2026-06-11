# Stage 4D-209N Recovery Trigger Queue Friendly Destroyed Destroyed Object Graveyard Location Count Audit

Date: 2026-06-12 04:35 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test slice covering spectator replay timing trigger queue Ghostly Centaur friendly-destroyed power destroyed-object graveyard-location validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardLocationContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative trigger `TRIGGER-stack-1-source-1-destroyed-1-GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, keeps `source-1` controlled by `alice`, located in `alice`'s `BASE`, listed in `alice`'s base zone, keeps `destroyed-1` listed in `alice`'s `GRAVEYARD` and registered as a unit card, but records `destroyed-1` as zone `BASE` in authoritative object locations, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation rejects the frame with the Ghostly Centaur friendly-destroyed power destroyed-object graveyard-location diagnostic, unexpected-trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardLocationContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1746/1746`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1751/1751`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8021/8021`.
- Mechanical: `git diff --check` passed.
- Conflict-marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

Main code commit: `9b46ea2c`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT remained clean at `17bde0c3` before this docs sync, observed on 2026-06-12 04:35 CST. Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

This narrows recovery spectator replay timing trigger queue Ghostly Centaur friendly-destroyed power destroyed-object graveyard-location validation with count mismatch only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
