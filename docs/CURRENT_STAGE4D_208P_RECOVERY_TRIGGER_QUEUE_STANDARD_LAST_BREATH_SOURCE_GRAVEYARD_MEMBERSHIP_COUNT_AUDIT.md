# Stage 4D-208P Recovery Trigger Queue Standard Last Breath Source Graveyard Membership Count Audit

Date: 2026-06-12

Status: accepted on main. Project remains **NOT READY**.

## Summary

A_MAIN added one direct single-agent server-test slice for spectator replay timing trigger queue standard last-breath source graveyard membership validation while the spectator trigger queue count also mismatches authoritative state.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test starts from authoritative `TRIGGER-stack-1-source-1-WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, keeps `source-1` controlled by `alice`, registered as card `OGN·096/298`, and located in `alice`'s `GRAVEYARD`, but leaves `source-1` absent from `alice`'s authoritative graveyard zone list. It then appends `trigger-extra` to the spectator replay timing trigger queue to force trigger queue count and key-set drift.

The recovery validator is now locked to emit the Watchful Sentinel last-breath draw source graveyard membership diagnostic plus unexpected-trigger-id and trigger queue count mismatch diagnostics in the same redacted spectator frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1722/1722`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1727/1727`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7997/7997`.
- Mechanical checks: `git diff --check` passed; conflict-marker scan over `docs`, `tests`, and `src` found no markers.

## Coordination

Main code commit: `40429ee3`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before this docs sync.

## Remaining Gates

This narrows recovery spectator replay timing trigger queue standard last-breath source graveyard membership validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
