# Stage 4D-210T Recovery Trigger Queue Watchful Sentinel Context Count Audit

Date: 2026-06-12 12:42 CST

Project status: **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for spectator replay recovery timing trigger queue validation. Runtime changed: no, server test coverage only.

This slice covers Watchful Sentinel last-breath draw context validation while the spectator trigger queue count also mismatches authoritative state.

## Coverage Added

- File: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Test: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueWatchfulSentinelLastBreathDrawContextDriftWithCountMismatch`
- Main code commit: `9cb3b36c`

The test starts from authoritative trigger id `TRIGGER-stack-1-source-1-WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, keeps `source-1` in `alice`'s `BASE` with authoritative object location zone `BASE`, rewrites the spectator trigger source visibility to `HIDDEN`, effect kind to `WRONG_EFFECT`, and triggered event kind to `CARD_PLAYED`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation must reject the replay frame with all of:

- Watchful Sentinel last-breath draw source visibility must be `VISIBLE`.
- Watchful Sentinel last-breath draw effect kind `WRONG_EFFECT` must be `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`.
- Watchful Sentinel last-breath draw triggered event kind `CARD_PLAYED` must be `UNIT_DESTROYED`.
- Spectator trigger id `trigger-extra` is not present in authoritative state trigger queue.
- Spectator trigger queue count `2` does not match authoritative state trigger queue count `1`.

The local scan for unpaired `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueue*ContextDrift` tests now has no remaining missing `WithCountMismatch` pair.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueWatchfulSentinelLastBreathDrawContextDriftWithCountMismatch"` -> `1/1`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1778/1778`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1783/1783`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8053/8053`
- Mechanical: `git diff --check` passed.
- Conflict scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

- No subagent or new worktree was created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
- Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth and final readiness remain open.
