# Stage 4D-206L Recovery Temporary Payment Resource Trait Property Name Audit

Date: 2026-06-11 15:46 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one server-test-only recovery slice for spectator replay timing temporary payment resource keyed power-trait map property-name validation without a temporary payment resource count mismatch.

Runtime changed: no. The slice only adds `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedPowerTraitMapPropertyNameWithoutCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Coverage

The new test keeps one spectator temporary payment resource keyed to authoritative `temp-payment-resource-1`, then serializes that single item as raw JSON with duplicate `red`, surrounding-whitespace `blue`, and blank trait property names in both `generatedPowerByTrait` and `remainingPowerByTrait` without adding or removing temporary resources.

It proves recovery validation reports generated/remaining power-trait duplicate-property, surrounding-whitespace-property, required-property-name diagnostics, keyed generated/remaining trait mismatch diagnostics, and aggregate same-count generated/remaining trait disagreement while proving no temporary payment resource count diagnostic, no id aggregate drift, no missing/unexpected resource-id key-set diagnostics, no source-object aggregate drift, no owner aggregate drift, no ability-id aggregate drift, no payment-window aggregate drift, no generated-power aggregate drift, no remaining-power aggregate drift, no allowed-payment-kind aggregate drift, no payment-only aggregate drift, no restriction aggregate drift and no created-tick aggregate drift.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedPowerTraitMapPropertyNameWithoutCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1666/1666`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1671/1671`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7941/7941`.
- Mechanical: `git diff --check` passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Coordination

Main code commit: `1fb07613`.

No subagent or new worktree was created. DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-11 15:46 CST; no new handoff was open.

Runtime validation code, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
