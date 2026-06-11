# Stage 4D-207M Recovery Trigger Queue OGS Lux Source Object Count Audit

Date: 2026-06-11 20:08 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue OGS Lux high-cost spell source-object context validation with a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceObjectContextDriftWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test starts from the authoritative OGS Lux high-cost spell trigger keyed as `TRIGGER-stack-1-source-1-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`, changes the keyed spectator trigger item `sourceObjectId` to `source-2` while the trigger id still encodes `source-1`, then appends a second trigger item with `triggerId` `trigger-extra` to force trigger queue count and key-set drift.

It proves recovery validation reports the OGS Lux high-cost spell source-object/trigger-id context diagnostic alongside unexpected trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceObjectContextDriftWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1693/1693`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1698/1698`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7968/7968`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `fcdb2132`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 20:08 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
