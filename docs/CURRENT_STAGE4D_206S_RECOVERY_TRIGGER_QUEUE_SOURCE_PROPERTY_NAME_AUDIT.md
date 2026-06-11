# Stage 4D-206S Recovery Trigger Queue Source Property Name Audit

Date: 2026-06-11 17:01 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue keyed source property-name validation without a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedSourcePropertyNameWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator trigger queue item keyed to authoritative `trigger-1`, then serializes that keyed item as raw JSON with duplicate `sourceObjectId`, surrounding-whitespace `sourceVisibility` property name and a blank property name. It preserves the canonical source object id while omitting the canonical `sourceVisibility` property, and it keeps the trigger queue count unchanged.

It proves recovery validation reports duplicate-property, surrounding-whitespace-property, required-property-name, keyed authoritative source-visibility mismatch and aggregate same-count source-visibility disagreement diagnostics. It also proves the source-object aggregate and trigger queue count diagnostics stay absent for this same-count source-visibility-only property-name drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedSourcePropertyNameWithoutCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1673/1673`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1678/1678`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7948/7948`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `e4319739`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 17:01 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
