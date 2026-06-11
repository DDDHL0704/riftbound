# Stage 4D-208V Recovery Trigger Queue Sad Poro Context Count Audit

Date: 2026-06-12

Status: accepted on main. Project remains **NOT READY**.

## Summary

A_MAIN added one direct single-agent server-test slice for spectator replay timing trigger queue Sad Poro last-breath draw context validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSadPoroLastBreathDrawContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative `TRIGGER-stack-1-source-1-SAD_PORO_LAST_BREATH_DRAW_1`, keeps `source-1` controlled by `alice`, located in `alice`'s `BASE`, and listed in `alice`'s base zone. It changes the spectator trigger item's `sourceVisibility` to `HIDDEN`, `effectKind` to `WRONG_EFFECT`, and `triggeredByEventKind` to `CARD_PLAYED`, then appends `trigger-extra` to force trigger queue count and key-set drift.

The recovery validator is now locked to emit the Sad Poro last-breath draw source-visibility, effect-kind, triggered-event, unexpected-trigger-id and trigger queue count mismatch diagnostics in the same redacted spectator frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSadPoroLastBreathDrawContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1728/1728`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1733/1733`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8003/8003`.
- Mechanical checks: `git diff --check` passed; conflict-marker scan over `docs`, `tests`, and `src` found no markers.

## Coordination

Main code commit: `d30eba19`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before this docs sync.

## Remaining Gates

This narrows recovery spectator replay timing trigger queue Sad Poro last-breath draw context validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
