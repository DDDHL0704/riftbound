# Stage 4D-205C Recovery Temporary Payment Resource Remaining Power Null Audit

Date: 2026-06-11 10:03 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing temporary payment resource keyed remaining-power null-value validation without a temporary payment resource count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRemainingPowerNullValueWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator temporary payment resource keyed to authoritative `temp-payment-resource-1`, then changes only `remainingPower` to `null` without adding or removing temporary resources.

It proves recovery validation reports remaining-power required diagnostics, keyed authoritative remaining-power mismatch diagnostics and aggregate same-count remaining-power disagreement while proving no remaining-power invalid diagnostic, no temporary payment resource count diagnostic, no missing/unexpected resource-id key-set diagnostics, no source-object aggregate drift, no owner aggregate drift, no ability-id aggregate drift, no payment-window aggregate drift, no generated-power aggregate drift, no generated-power-trait aggregate drift and no remaining-power-trait aggregate drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRemainingPowerNullValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1631/1631`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1636/1636`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7906/7906`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `adcc8a8d`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3`; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
