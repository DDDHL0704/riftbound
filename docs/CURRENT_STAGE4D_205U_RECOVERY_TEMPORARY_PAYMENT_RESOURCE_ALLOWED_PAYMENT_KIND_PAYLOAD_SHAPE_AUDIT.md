# Stage 4D-205U Recovery Temporary Payment Resource Allowed Payment Kind Payload Shape Audit

Date: 2026-06-11 12:41 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing temporary payment resource keyed allowed-payment-kind-list payload-shape validation without a temporary payment resource count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListPayloadShapeWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator temporary payment resource keyed to authoritative `temp-payment-resource-1`, then changes only `allowedPaymentKinds` to object-shaped raw JSON `{"kind":"RUNE_COST"}` without adding or removing temporary resources.

It proves recovery validation reports allowed-payment-kind-list payload-required diagnostics, allowed-payment-kind-list invalid diagnostics, keyed authoritative allowed-payment-kind mismatch diagnostics and aggregate same-count allowed-payment-kind disagreement while proving no empty allowed-payment-kind required diagnostic, no surrounding-whitespace diagnostic, no duplicate allowed-payment-kind diagnostic, no temporary payment resource count diagnostic, no id aggregate drift, no missing/unexpected resource-id key-set diagnostics, no source-object aggregate drift, no owner aggregate drift, no ability-id aggregate drift, no payment-window aggregate drift, no generated-power aggregate drift, no remaining-power aggregate drift, no generated-power-trait aggregate drift, no remaining-power-trait aggregate drift, no payment-only aggregate drift, no restriction aggregate drift and no created-tick aggregate drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListPayloadShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1649/1649`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1654/1654`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7924/7924`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `386bf9b9`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3`; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
