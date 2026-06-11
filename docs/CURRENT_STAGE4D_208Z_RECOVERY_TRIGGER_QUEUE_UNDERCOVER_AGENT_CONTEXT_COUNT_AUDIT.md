# Stage 4D-208Z Recovery Trigger Queue Undercover Agent Context Count Audit

Date: 2026-06-12 02:25 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test slice covering spectator replay timing trigger queue Undercover Agent last-breath context validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueUndercoverAgentLastBreathContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative trigger `TRIGGER-stack-1-source-1-UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`, keeps `source-1` controlled by `alice`, located in `alice`'s `BASE`, and listed in `alice`'s base zone, changes the spectator trigger item's `sourceVisibility` to `HIDDEN`, `effectKind` to `WRONG_EFFECT`, and `triggeredByEventKind` to `CARD_PLAYED`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation rejects the frame with Undercover Agent last-breath source-visibility, effect-kind, triggered-event, unexpected-trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueUndercoverAgentLastBreathContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1732/1732`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1737/1737`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8007/8007`.
- Mechanical: `git diff --check` passed.
- Conflict-marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

Main code commit: `2ad8af99`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT remained clean at `17bde0c3` before this docs sync. Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

This narrows recovery spectator replay timing trigger queue Undercover Agent last-breath context validation with count mismatch only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
