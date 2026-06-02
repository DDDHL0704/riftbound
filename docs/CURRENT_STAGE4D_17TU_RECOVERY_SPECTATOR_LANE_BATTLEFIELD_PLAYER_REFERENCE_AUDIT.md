# Stage 4D-17TU Recovery Spectator Lane Battlefield Player-Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TU tightens spectator replay-frame snapshot lane battlefield validation for player references under lane battlefield count mismatch. This slice adds same-payload seat membership diagnostics before the existing broad lane scalar/list parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates spectator lane `battlefields[]` player references against authoritative `MatchState.Seats`.

Covered fields:

- `zonePlayerId`
- optional `controllerId`
- `occupantControllerIds[]`
- `unitsBySide` player keys
- `scoredThisTurnPlayerIds[]`

The existing lane payload shape validation, lane battlefield count mismatch diagnostic, broad scalar/list parity diagnostics and downstream standby-slot diagnostics remain intact. Standby slot player references are intentionally left as a separate follow-up slice.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldPlayerReferencesOutsideSeatsWithCountMismatch`

The test builds a spectator replay frame with one authoritative battlefield and one visible unit, forges player ids outside seats across scalar, list and map-key lane battlefield fields, and verifies explicit missing-seat diagnostics plus the existing lane battlefield count and broad mismatch diagnostics.

Validation passed:

- focused player-reference test `1/1`
- focused SpectatorReplaySnapshotLane filter `7/7`
- focused SpectatorReplaySnapshotBattlefield filter `6/6`
- focused recovery `648/648`
- adjacent recovery/opening/store-smoke filter `1229/1229`
- backend full `6594/6594`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Additional mechanical note:

- Known global `dotnet format Riftbound.slnx --no-restore --verify-no-changes` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
