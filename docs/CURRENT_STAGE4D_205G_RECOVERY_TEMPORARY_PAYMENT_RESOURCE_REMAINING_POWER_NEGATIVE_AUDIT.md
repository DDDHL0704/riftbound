# Stage 4D-205G Recovery Temporary Payment Resource Remaining Power Negative Audit

Date: 2026-06-11 10:35 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing temporary payment resource keyed remaining-power negative-value validation without a temporary payment resource count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRemainingPowerNegativeValueWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator temporary payment resource keyed to authoritative `temp-payment-resource-1`, then changes only `remainingPower` to `-1` without adding or removing temporary resources.

It proves recovery validation reports remaining-power negative-value diagnostics, keyed authoritative remaining-power mismatch diagnostics and aggregate same-count remaining-power disagreement while proving no remaining-power required/invalid diagnostic, no temporary payment resource count diagnostic, no missing/unexpected resource-id key-set diagnostics, no source-object aggregate drift, no owner aggregate drift, no ability-id aggregate drift, no payment-window aggregate drift, no generated-power aggregate drift, no generated-power-trait aggregate drift and no remaining-power-trait aggregate drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRemainingPowerNegativeValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1635/1635`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1640/1640`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7910/7910`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `e3614a37`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3`; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
