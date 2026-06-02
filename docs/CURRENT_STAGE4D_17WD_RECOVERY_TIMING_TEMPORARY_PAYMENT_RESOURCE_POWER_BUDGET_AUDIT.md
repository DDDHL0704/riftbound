# Stage 4D-17WD Recovery Timing Temporary Payment Resource Power Budget Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightened `MatchRecoveryValidator` temporary payment resource validation for recovered snapshots, authoritative state and spectator replay frames. `temporaryPaymentResources[]` entries now reject scalar and trait budget drift where retained remaining power exceeds generated power.

Runtime temporary payment resources are generated once and then only spend down during payment. Legal recovery/replay state therefore cannot carry `remainingPower` greater than `generatedPower`, cannot carry a remaining trait budget without a corresponding generated trait budget, and cannot carry a remaining trait value greater than that trait's generated value.

## Files

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Coverage

- `RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourcePowerBudgetDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTemporaryPaymentResourcePowerBudgetDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcePowerBudgetDrift`

These tests cover scalar remaining/generated drift, remaining trait budget greater than generated trait budget, and remaining trait budget with no generated trait budget in recovered snapshot timing payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new temporary-payment-resource power-budget tests: `3/3`.
- Focused `TemporaryPaymentResource` filter: `72/72`.
- Focused recovery filter: `771/771`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1351/1351`.
- Backend full: `6716/6716`.
- Touched-file scoped whitespace format passed.
- `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism and temporary payment resource budget compatibility. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness.
