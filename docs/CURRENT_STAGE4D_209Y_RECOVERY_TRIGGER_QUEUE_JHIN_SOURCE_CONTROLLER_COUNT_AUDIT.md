# Stage 4D-209Y Recovery Trigger Queue Jhin Source Controller Count Audit

Date: 2026-06-12 10:16 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test slice covering spectator replay timing trigger queue Jhin movement resource source-controller validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceControllerContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative trigger `JHIN_MOVE_RESOURCE::3::source-1::BASE::BATTLEFIELD`, keeps `source-1` as the valid Jhin unit card controlled by `alice`, keeps `wrong-source` as a Jhin unit card controlled by `bob` and located in `bob`'s `BATTLEFIELD`, rewrites the spectator trigger item to forged trigger id `JHIN_MOVE_RESOURCE::3::wrong-source::BASE::BATTLEFIELD`, sets `sourceObjectId` to `wrong-source`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation rejects the frame with the Jhin movement resource source-controller diagnostic, plus unexpected-trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceControllerContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1757/1757`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1762/1762`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8032/8032`.
- Mechanical: `git diff --check` passed.
- Conflict-marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

Main code commit: `15824d19`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT remained clean at `17bde0c3` before this docs sync, observed on 2026-06-12 10:16 CST. Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

This narrows recovery spectator replay timing trigger queue Jhin movement resource source-controller validation with count mismatch only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
