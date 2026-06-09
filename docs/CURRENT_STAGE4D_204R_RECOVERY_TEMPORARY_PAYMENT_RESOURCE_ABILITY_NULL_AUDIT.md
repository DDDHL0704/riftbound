# Stage 4D-204R Recovery Temporary Payment Resource Ability Null Audit

Date: 2026-06-10 07:39 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing temporary payment resource keyed ability-id null-value validation without a temporary payment resource count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAbilityIdNullValueWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator temporary payment resource keyed to authoritative `temp-payment-resource-1`, then changes only `abilityId` to `null` without adding or removing temporary resources.

It proves recovery validation reports keyed authoritative ability-id mismatch diagnostics and aggregate same-count ability-id disagreement while proving no ability-id required diagnostic, no temporary payment resource count diagnostic, no missing/unexpected resource-id key-set diagnostics, no source-object aggregate drift, no owner aggregate drift and no generated-power aggregate drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAbilityIdNullValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1620/1620`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1625/1625`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7895/7895`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `784253f5`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3`; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
