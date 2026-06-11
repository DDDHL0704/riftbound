# Stage 4D-205Q Recovery Temporary Payment Resource Allowed Payment Kind Null Audit

Date: 2026-06-11 11:55 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing temporary payment resource keyed allowed-payment-kind-list null-value validation without a temporary payment resource count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListNullValueWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator temporary payment resource keyed to authoritative `temp-payment-resource-1`, then changes only `allowedPaymentKinds` to `null` without adding or removing temporary resources.

It proves recovery validation reports keyed authoritative allowed-payment-kind mismatch diagnostics and aggregate same-count allowed-payment-kind disagreement while proving no allowed-payment-kind-list payload-required diagnostic, no allowed-payment-kind-list invalid diagnostic, no empty allowed-payment-kind required diagnostic, no temporary payment resource count diagnostic, no id aggregate drift, no missing/unexpected resource-id key-set diagnostics, no source-object aggregate drift, no owner aggregate drift, no ability-id aggregate drift, no payment-window aggregate drift, no generated-power aggregate drift, no remaining-power aggregate drift, no generated-power-trait aggregate drift, no remaining-power-trait aggregate drift, no payment-only aggregate drift, no restriction aggregate drift and no created-tick aggregate drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListNullValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1645/1645`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1650/1650`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7920/7920`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `4cb79452`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3`; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
