# Stage 4D-17TZ Recovery Spectator Lane Battlefield Status Known-Value Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted checkpoint slice. Project remains **NOT READY**.

## Scope

Stage 4D-17TZ tightens spectator replay-frame snapshot lane battlefield scalar validation under lane battlefield count mismatch. This slice adds an explicit known-value diagnostic for unknown battlefield `status` values before existing authoritative parity diagnostics continue.

## Runtime Change

Changed `src/Riftbound.Engine/MatchRecovery.cs`.

`MatchRecoveryValidator` now validates spectator replay-frame snapshot lane `battlefields[]` `status` values with the shared `IsKnownBattlefieldStatus` predicate. This matches the recovered player-view lane battlefield status guard and preserves the existing authoritative status mismatch diagnostic.

## Test Coverage

Changed `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Added:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldStatusKnownValueWithCountMismatch`

The test forges spectator `status = "UNKNOWN"` under lane battlefield count mismatch and verifies the explicit invalid-status diagnostic plus the existing lane battlefield count and authoritative status mismatch diagnostics.

## Validation

- focused status known-value test `1/1`
- focused SpectatorReplaySnapshotLane filter `12/12`
- focused SpectatorReplaySnapshotBattlefield filter `6/6`
- focused recovery `653/653`
- adjacent recovery/opening/store-smoke `1234/1234`
- backend full `6599/6599`
- `git diff --check` passed
- anchored conflict-marker scan over `docs`, `src` and `tests` passed
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed

Known global `dotnet format Riftbound.slnx --verify-no-changes` remains blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Locked Surfaces

No matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status or `riftbound-dotnet.sln` changes.
