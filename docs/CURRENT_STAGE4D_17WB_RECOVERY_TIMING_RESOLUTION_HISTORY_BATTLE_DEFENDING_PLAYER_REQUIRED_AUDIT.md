# Stage 4D-17WB Recovery Timing Resolution-History Battle Defending Player Required Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightened `MatchRecoveryValidator` battle-resolution role validation for recovered snapshots, authoritative state and spectator replay frames. `battleResolutions[]` entries now require a non-blank `defendingPlayerId` before attacker/defender/winner role compatibility checks continue.

Runtime battle declaration requires non-empty defender object ids, and retained battle history records `defendingPlayerId` alongside `defenderObjectIds`. A recovered `CLOSED` or `NO_RESULT` battle-resolution payload carrying defender participants without the defending player scalar is therefore recovery/replay drift, not legal runtime state.

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

- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryBattleDefendingPlayerRequiredDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryBattleDefendingPlayerRequiredDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryBattleDefendingPlayerRequiredDrift`

These tests cover missing, null and blank `defendingPlayerId` drift in snapshot timing payloads, authoritative state resolution history and spectator replay-frame timing payloads.

## Validation

- Focused new defending-player required tests: `3/3`.
- Focused `ResolutionHistory` filter: `102/102`.
- Focused recovery filter: `765/765`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1345/1345`.
- Backend full: `6710/6710`.
- Touched-file scoped whitespace format passed.
- `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism and battle-resolution role scalar validation. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness.
