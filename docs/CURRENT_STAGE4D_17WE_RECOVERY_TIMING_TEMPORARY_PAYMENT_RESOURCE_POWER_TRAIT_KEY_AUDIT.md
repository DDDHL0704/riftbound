# Stage 4D-17WE Recovery Timing Temporary Payment Resource Power Trait Key Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightened `MatchRecoveryValidator` temporary payment resource validation for recovered snapshots, authoritative state and spectator replay frames. `temporaryPaymentResources[]` `generatedPowerByTrait` and `remainingPowerByTrait` maps now reject non-canonical rune trait keys, unknown rune trait keys and keys that collide after `RuneTrait.Normalize`.

Runtime `TemporaryPaymentResourceState` normalizes and merges trait maps through `PaymentCostRules.NormalizePowerCostByTrait`. Legal snapshot/redactor output therefore carries canonical trait keys only; aliases, case drift, unknown traits and normalization collisions are recovery/replay drift.

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

- `RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourcePowerTraitKeyDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTemporaryPaymentResourcePowerTraitKeyDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcePowerTraitKeyDrift`

These tests cover non-canonical case drift, normalized duplicate keys and unknown trait keys in recovered snapshot timing payloads, authoritative state and spectator replay-frame timing payloads.

## Validation

- Focused new temporary-payment-resource power-trait key tests: `3/3`.
- Focused `TemporaryPaymentResource` filter: `75/75`.
- Focused recovery filter: `774/774`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1354/1354`.
- Backend full: `6719/6719`.
- Touched-file scoped whitespace format passed.
- `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism and temporary payment resource trait-key compatibility. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness.
