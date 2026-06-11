# Stage 4D-208I Recovery Trigger Queue Standard Last Breath Source Unit Card Count Audit

Date: 2026-06-11 23:52 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue standard last-breath source unit-card validation with a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceUnitCardContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test starts from the authoritative Scouting Warhawk last-breath call-rune trigger keyed as `TRIGGER-stack-1-source-1-SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`, keeps `source-1` controlled by `alice`, located in `alice`'s `GRAVEYARD`, and present in `alice`'s graveyard zone list, but leaves its authoritative card-object tags empty while preserving card number `OGN·216/298`. It then appends a second trigger item with `triggerId` `trigger-extra` to force trigger queue count and key-set drift.

It proves recovery validation reports the Scouting Warhawk last-breath call-rune source unit-card diagnostic alongside unexpected trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceUnitCardContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1715/1715`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1720/1720`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7990/7990`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `7a2b5a0d`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 23:52 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
