# Stage 4D-206N Recovery Trigger Queue Property Name Count Audit

Date: 2026-06-11 16:09 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue keyed property-name validation with a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedPropertyNameWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator trigger queue item keyed to authoritative `trigger-1`, then serializes that keyed item as raw JSON with duplicate `triggerId`, surrounding-whitespace `controllerId` property name and a blank property name. It also appends a second redacted trigger item retagged as `trigger-extra` to force trigger queue count and key-set drift.

It proves recovery validation reports duplicate-property, surrounding-whitespace-property, required-property-name, keyed authoritative controller mismatch, unexpected trigger-id and trigger queue count mismatch diagnostics.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedPropertyNameWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1668/1668`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1673/1673`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7943/7943`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `c5334c9d`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 16:09 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
