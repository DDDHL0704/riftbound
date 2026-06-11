# Stage 4D-209Q Recovery Trigger Queue Viktor Destroyed Non-Minion Destroyed Object Minion Family Count Audit

Date: 2026-06-12 05:04 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test slice covering spectator replay timing trigger queue Viktor destroyed non-minion create-minion destroyed-object minion-family validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectMinionFamilyContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative trigger `TRIGGER-stack-1-source-1-destroyed-1-VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, keeps `source-1` controlled by `alice`, located in `alice`'s `BASE`, listed in `alice`'s base zone, keeps `destroyed-1` in `alice`'s `GRAVEYARD`, but registers that destroyed object as a unit card in the minion token family, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation rejects the frame with the Viktor destroyed non-minion create-minion destroyed-object minion-family diagnostic, unexpected-trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectMinionFamilyContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1749/1749`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1754/1754`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8024/8024`.
- Mechanical: `git diff --check` passed.
- Conflict-marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

Main code commit: `bb08b3b6`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT remained clean at `17bde0c3` before this docs sync, observed on 2026-06-12 05:04 CST. Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

This narrows recovery spectator replay timing trigger queue Viktor destroyed non-minion create-minion destroyed-object minion-family validation with count mismatch only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
