# Stage 4D-210E Recovery Trigger Queue Jhin Destination Base Count Audit

Date: 2026-06-12 11:03 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test slice covering spectator replay timing trigger queue Jhin movement resource destination BASE object-location validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceDestinationBaseLocationContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative trigger `JHIN_MOVE_RESOURCE::3::source-1::BATTLEFIELD:battlefield-1::BASE`, keeps `source-1` as a Jhin unit card controlled by `alice`, keeps `source-1` in authoritative object locations as `BATTLEFIELD:battlefield-1`, keeps `battlefield-1` and `battlefield-2` as battlefield card objects in `alice`'s `Battlefields`, preserves the spectator trigger item destination `BASE`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation rejects the frame with the Jhin movement resource destination `BASE` does not match source object id `source-1` location `BATTLEFIELD` diagnostic, plus unexpected-trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceDestinationBaseLocationContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1763/1763`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1768/1768`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8038/8038`.
- Mechanical: `git diff --check` passed.
- Conflict-marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

Main code commit: `b53dbab0`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT remained clean at `17bde0c3` before this docs sync, observed on 2026-06-12 11:03 CST. Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

This narrows recovery spectator replay timing trigger queue Jhin movement resource destination BASE object-location validation with count mismatch only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
