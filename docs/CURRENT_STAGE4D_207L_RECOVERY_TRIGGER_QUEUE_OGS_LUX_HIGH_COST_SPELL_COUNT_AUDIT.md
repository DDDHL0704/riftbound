# Stage 4D-207L Recovery Trigger Queue OGS Lux High Cost Spell Count Audit

Date: 2026-06-11 19:59 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue OGS Lux high-cost spell context validation with a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test starts from the authoritative OGS Lux high-cost spell trigger keyed as `TRIGGER-stack-1-source-1-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`, changes the keyed spectator trigger item `effectKind` to `WRONG_EFFECT` and `triggeredByEventKind` to `UNIT_DESTROYED`, then appends a second trigger item with `triggerId` `trigger-extra` to force trigger queue count and key-set drift.

It proves recovery validation reports the OGS Lux high-cost spell effect-kind and triggered-event diagnostics alongside unexpected trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1692/1692`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1697/1697`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7967/7967`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `39384bb7`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 19:59 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
