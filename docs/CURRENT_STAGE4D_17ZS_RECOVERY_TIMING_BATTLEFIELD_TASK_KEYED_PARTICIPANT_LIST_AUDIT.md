# Stage 4D-17ZS Recovery Timing Battlefield Task Keyed Participant List Audit

Date: 2026-06-04 03:37 CST

Owner: A_MAIN

Status: accepted for this slice only. Project remains **NOT READY**.

## Scope

This slice tightens `MatchRecoveryValidator` spectator replay-frame timing validation for `battlefieldTasks[]` entries. When spectator task count differs from authoritative `MatchState.BattlefieldTasks`, broad ordered parity returns early, so same-key task entries must carry their own authoritative value checks.

The keyed battlefield-task validator now compares the spectator payload for the same `(battlefieldObjectId, kind)` against authoritative:

- `reason`
- `participantControllerIds[]`
- `participantObjectIds[]`
- existing `status`, `actingPlayerId` and `stackItemIds[]`

No protocol shape, frontend, matrix JSON, official catalog or command-resolution behavior changed.

## Runtime Parity

`MatchState.BattlefieldTasks` builds deterministic task views from current contested battlefield state: participant object ids are sorted unit occupants, participant controller ids are sorted distinct effective controllers, and task reason is fixed by task kind. `MatchReplayRedactor.BuildSpectatorFrame` serializes that same view into replay timing.

Before this slice, count mismatch still emitted task count, kind-set, derived identity and object-membership diagnostics, but same-key participant ordering/value drift could avoid the ordered parity check. The new keyed checks reject that drift directly for the matching battlefield task key.

## Tests

New coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKeyedParticipantListsWithCountMismatch`

Validation passed:

- focused new battlefield-task keyed participant-list test `1/1`
- focused `BattlefieldTask` filter `60/60`
- focused recovery filter `1047/1047`
- adjacent recovery/official-opening/Postgres recovery-store filter `1628/1628`
- backend full `6993/6993`
- touched-file scoped whitespace format

Mechanical checks to close the commit:

- `git diff --check`
- anchored conflict-marker scan over `docs`, `tests`, `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

Files intentionally touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current Stage 4D checkpoint/completion/P0-P1/dispatch/shared-board docs
- this audit document

Still locked: matrix JSON writes except validation parse, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.
