# Stage 4D-17TV Recovery Spectator Lane Standby-Slot Player-Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TV tightens spectator replay-frame snapshot lane standby-slot validation for player references under lane battlefield count mismatch. This slice adds same-payload seat membership diagnostics before the existing standby-slot value parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates spectator lane `battlefields[].standbySlots[]` optional player references against authoritative `MatchState.Seats`.

Covered fields:

- `sidePlayerId`
- `controllerId`

The existing lane payload shape validation, lane battlefield count mismatch diagnostic, standby-slot scalar/value parity diagnostics and visible/hidden standby object redaction checks remain intact. This follows the 17TU lane battlefield player-reference slice and covers the standby-slot player fields that were intentionally left for a follow-up.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneStandbySlotPlayerReferencesOutsideSeatsWithCountMismatch`

The test builds a spectator replay frame with one authoritative battlefield and one visible standby object, forges standby slot `sidePlayerId` and `controllerId` outside seats, and verifies explicit missing-seat diagnostics plus the existing standby-slot mismatch and lane battlefield count mismatch diagnostics.

Validation passed:

- focused standby-slot player-reference test `1/1`
- focused SpectatorReplaySnapshotLane filter `8/8`
- focused SpectatorReplaySnapshotBattlefield filter `6/6`
- focused recovery `649/649`
- adjacent recovery/opening/store-smoke filter `1230/1230`
- backend full `6595/6595`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Additional mechanical note:

- Known global `dotnet format Riftbound.slnx --no-restore --verify-no-changes` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
