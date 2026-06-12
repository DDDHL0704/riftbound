# Stage 4D-210D Recovery Trigger Queue Jhin Origin Battlefield Count Audit

Date: 2026-06-12 10:57 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test slice covering spectator replay timing trigger queue Jhin movement resource origin battlefield-state validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceOriginBattlefieldStateContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative trigger `JHIN_MOVE_RESOURCE::3::source-1::BATTLEFIELD:battlefield-1::BASE`, keeps `source-1` as a Jhin unit card controlled by `alice`, keeps `battlefield-1` in `alice`'s `Battlefields` and object locations as the authoritative battlefield state, keeps `battlefield-2` in `alice`'s `BASE` and object locations as `BASE` while tagged as a battlefield card, rewrites the spectator trigger item to forged trigger id `JHIN_MOVE_RESOURCE::3::source-1::BATTLEFIELD:battlefield-2::BASE`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation rejects the frame with the Jhin movement resource origin battlefield object id `battlefield-2` missing from authoritative state battlefield states diagnostic, plus unexpected-trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceOriginBattlefieldStateContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1762/1762`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1767/1767`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8037/8037`.
- Mechanical: `git diff --check` passed.
- Conflict-marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

Main code commit: `77066836`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT remained clean at `17bde0c3` before this docs sync, observed on 2026-06-12 10:57 CST. Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

This narrows recovery spectator replay timing trigger queue Jhin movement resource origin battlefield-state validation with count mismatch only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
