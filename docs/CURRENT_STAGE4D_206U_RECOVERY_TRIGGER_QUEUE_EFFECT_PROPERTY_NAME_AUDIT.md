# Stage 4D-206U Recovery Trigger Queue Effect Property Name Audit

Date: 2026-06-11 17:22 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing trigger queue keyed effect property-name validation without a trigger queue count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedEffectPropertyNameWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator trigger queue item keyed to authoritative `trigger-1`, then serializes that keyed item as raw JSON with duplicate `effectKind`, surrounding-whitespace `triggeredByEventKind` property name and a blank property name. It preserves the canonical `effectKind` while omitting the canonical `triggeredByEventKind` property, and keeps the trigger queue count aligned with authoritative state.

It proves recovery validation reports duplicate-property, surrounding-whitespace-property, required-property-name, keyed authoritative triggered-event-kind mismatch and aggregate same-count triggered-event-kind disagreement diagnostics, while proving no effect-kind aggregate drift and no trigger queue count mismatch diagnostic.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedEffectPropertyNameWithoutCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1675/1675`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1680/1680`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7950/7950`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `a975cc64`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 17:22 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
