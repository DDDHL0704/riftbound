# Stage 4D-18ME/18MF/18MG Official Layer Recovery Audit

Date: 2026-06-06 04:32 CST

Status: accepted into A_MAIN pending checkpoint commit. Project remains **NOT READY**.

## Scope

This bundled server test slice integrated three parallel worker-produced changes:

- 18ME `df17fc16`: `OfficialOpeningTests` adds `GameCommandMapperTrimsOfficialDeckAndMulliganTextArrays`, proving official `SUBMIT_DECK` and `MULLIGAN` text arrays trim valid strings and drop blank/null/unreadable entries.
- 18MF `6df59461`: `LayerEngineTimestampDependencyTests` adds `LayerEngineStaticAuraDependencyMetadataDisappearsWhenSourceLeavesPublicFieldAcrossPlayerViews`, proving P1/P2 snapshots do not retain the removed Ornn static aura or stale dependency object ids after Ornn leaves the public field.
- 18MG `e5b38cb3`: `MatchRecoveryTests` adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceOriginBattlefieldStateContextDrift`, proving spectator replay timing trigger queues reject Jhin movement-resource origin battlefield-state drift.

Runtime changed: no. Matrix JSON changed: no. Frontend, browser/Chrome, official catalog, formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

## Validation

- Focused new tests: `3/3`.
- Touched class filter: `1896/1896`.
- Broader adjacent server filter: `5289/5289`.
- Backend full via tracked `Riftbound.slnx` under current no-DB environment: `7302/7302`.
- `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed before docs sync.

## Remaining Risk

This narrows official mapper, LayerEngine and recovery coverage only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
