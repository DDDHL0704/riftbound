# Stage 4D-207W Recovery Trigger Queue Teemo Stack Count Audit

Date: 2026-06-11 21:51 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue Teemo on-play self-power stack-context validation with a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerStackContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test starts from the authoritative Teemo on-play self-power trigger keyed as `TRIGGER-stack-1-TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`, changes the spectator trigger id to forged `TRIGGER--TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3` so it omits the runtime stack item id segment, then appends a second trigger item with `triggerId` `trigger-extra` to force trigger queue count and key-set drift.

It proves recovery validation reports the Teemo on-play self-power stack-context diagnostic alongside unexpected trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerStackContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1703/1703`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1708/1708`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7978/7978`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `6ea21871`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 21:51 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
