# Stage 4D-17TX Recovery Spectator Lane Battlefield Object-Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TX tightens spectator replay-frame snapshot lane battlefield list/dictionary object-reference validation under lane battlefield count mismatch. This slice adds same-payload object-list membership diagnostics before existing broad list parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates spectator lane `battlefields[]` object references in list/dictionary payloads.

Covered fields:

- `occupantObjectIds[]` against authoritative battlefield occupant object ids
- `unitsBySide` unit object values against authoritative battlefield occupant object ids
- `standbyObjectIds[]` against authoritative visible standby object ids

The existing lane payload shape validation, lane battlefield count mismatch diagnostic, player-reference checks, standby-slot checks and broad list parity diagnostics remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldObjectReferencesOutsideAuthoritativeListsWithCountMismatch`

The test builds a spectator replay frame with one authoritative battlefield, visible occupant units and one visible standby object, forges occupant, units-by-side and standby object ids outside the authoritative lists, and verifies explicit missing-authoritative-list diagnostics plus the existing lane battlefield count and broad list mismatch diagnostics.

Validation passed:

- focused battlefield object-reference test `1/1`
- focused SpectatorReplaySnapshotLane filter `10/10`
- focused SpectatorReplaySnapshotBattlefield filter `6/6`
- focused recovery `651/651`
- adjacent recovery/opening/store-smoke filter `1232/1232`
- backend full `6597/6597`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Additional mechanical note:

- Known global `dotnet format Riftbound.slnx --no-restore --verify-no-changes` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
