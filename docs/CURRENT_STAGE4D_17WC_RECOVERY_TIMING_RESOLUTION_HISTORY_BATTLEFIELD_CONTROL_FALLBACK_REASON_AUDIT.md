# Stage 4D-17WC Recovery Timing Resolution-History Battlefield Control Fallback Reason Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightened `MatchRecoveryValidator` battlefield control-resolution reason validation for recovered snapshots, authoritative state and spectator replay frames. `battlefieldResolutions[]` entries with `kind = CONTROL_RESOLVED` now reject fallback `reason = BATTLEFIELD_CONTROL_RESOLVED`.

Runtime `ResolveBattlefieldControlAfterCombat` writes concrete control outcomes on `BATTLEFIELD_CONTROL_RESOLVED` events: `UNCONTROLLED`, `CONTROL_CHANGED` or `CONTROL_CONFIRMED`. `AppendBattlefieldResolutionEvents` persists that payload `resolution` as the retained history reason before falling back to the event kind. A recovered control-resolution history item using the event kind as its reason is therefore recovery/replay drift, not legal runtime state.

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

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattlefieldControlFallbackReasonDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattlefieldControlFallbackReasonDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattlefieldControlFallbackReasonDrift`

These tests cover fallback control-resolution reason drift in snapshot timing payloads, authoritative state resolution history and spectator replay-frame timing payloads.

## Validation

- Focused new fallback-reason tests: `3/3`.
- Focused `ResolutionHistory` filter: `105/105`.
- Focused recovery filter: `768/768`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1348/1348`.
- Backend full: `6713/6713`.
- Touched-file scoped whitespace format passed.
- `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism and battlefield control-resolution reason compatibility. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness.
