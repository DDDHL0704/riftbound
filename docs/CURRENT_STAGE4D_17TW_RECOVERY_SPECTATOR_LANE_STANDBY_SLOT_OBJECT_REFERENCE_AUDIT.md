# Stage 4D-17TW Recovery Spectator Lane Standby-Slot Object-Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TW tightens spectator replay-frame snapshot lane standby-slot validation for visible object references under lane battlefield count mismatch. This slice adds same-payload visible-standby membership diagnostics before the existing exact visible standby parity diagnostic continues to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates visible spectator lane `battlefields[].standbySlots[]` `objectId` values against the authoritative visible standby object id set for the battlefield.

Covered field:

- visible-slot `objectId`

Hidden standby slots keep the existing redaction rule and are not object-reference validated through this path. The existing lane payload shape validation, lane battlefield count mismatch diagnostic, exact visible standby object mismatch diagnostic and hidden-object redaction check remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneStandbySlotObjectReferencesOutsideVisibleStandbyObjectsWithCountMismatch`

The test builds a spectator replay frame with one authoritative battlefield and one visible standby object, forges the visible standby slot `objectId` outside authoritative visible standby objects, and verifies explicit missing-visible-standby diagnostics plus the existing exact visible-standby mismatch and lane battlefield count mismatch diagnostics.

Validation passed:

- focused standby-slot object-reference test `1/1`
- focused SpectatorReplaySnapshotLane filter `9/9`
- focused SpectatorReplaySnapshotBattlefield filter `6/6`
- focused recovery `650/650`
- adjacent recovery/opening/store-smoke filter `1231/1231`
- backend full `6596/6596`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Additional mechanical note:

- Known global `dotnet format Riftbound.slnx --no-restore --verify-no-changes` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
