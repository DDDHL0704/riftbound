# Stage 4D-214E Recovery Spectator Trigger Queue Watchful Sentinel Last-Breath Draw Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Watchful Sentinel last-breath draw source-visibility/effect-kind/triggered-event-kind context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueWatchfulSentinelLastBreathDrawContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Watchful Sentinel last-breath draw trigger queue state.
- The test keeps the authoritative trigger queue count while the spectator trigger source visibility is `HIDDEN`, effect kind is `WRONG_EFFECT`, and triggered event kind is `CARD_PLAYED`.
- Recovery validation emits the Watchful Sentinel last-breath draw source visibility must be `VISIBLE` diagnostic.
- Recovery validation emits the Watchful Sentinel last-breath draw effect kind `WRONG_EFFECT` must be `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` diagnostic.
- Recovery validation emits the Watchful Sentinel last-breath draw triggered event kind `CARD_PLAYED` must be `UNIT_DESTROYED` diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same three Watchful Sentinel diagnostics together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueWatchfulSentinelLastBreathDrawContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `0266126d` (`test: cover spectator watchful sentinel trigger drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator watchful sentinel trigger drift`)

## Status

This narrows spectator replay timing trigger-queue Watchful Sentinel last-breath draw context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
