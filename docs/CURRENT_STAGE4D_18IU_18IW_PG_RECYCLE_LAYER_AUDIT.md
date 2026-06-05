# Stage 4D-18IU/18IV/18IW Postgres/Recycle/Layer Audit

Date: 2026-06-05

Status: accepted by A_MAIN for a main checkpoint. Project remains **NOT READY**.

## Scope

- 18IU: Postgres player-store seat uniqueness and seat-drift rejection.
- 18IV: Ornn P79 recycle event/main-deck order determinism.
- 18IW: LayerEngine battlefield static-aura participant filtering by `ObjectLocations.BattlefieldObjectId`.

## Worker Sources

- `dd49dd13` - Postgres player seat uniqueness.
- `7566c2df` - Ornn recycle order.
- `17b7ee1d` - LayerEngine battlefield location filter.

## Runtime Changes

- `PostgresMatchPlayerStore` now rejects another player taking an occupied seat and rejects a same-player seat change.
- Same player/same seat reconnect-token hash rotation remains accepted.
- New installs define `match_players` uniqueness on `(match_id, seat)`.
- Existing installs apply `src/Riftbound.Persistence/Sql/006_p1_match_player_seat_uniqueness.sql` to add the same constraint if absent.

## Test Changes

- `PostgresMatchRecoveryStoreSmokeTests` adds `PostgresMatchPlayerStoreRejectsSeatConflictsAndPlayerSeatDrift`.
- `ConformanceFixtureRunnerTests` locks raw `CARDS_RECYCLED.cardIds` order plus resulting main-deck order for Ornn select and decline paths.
- `LayerEngineTimestampDependencyTests` adds `LayerEngineBattlefieldStaticAuraUsesObjectLocationsToExcludeOtherBattlefields`.

## Validation

- Focused bundle: `9/9`.
- Adjacent combined server filter: `3051/3051`.
- Backend full: `7234/7234`.
- `git diff --check`: passed.
- `git diff --cached --check`: passed.
- Anchored conflict-marker scan: passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

## Remaining Risks

- Real DB-backed Postgres seat-conflict smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Migration `006` will fail on existing databases that already contain duplicate `(match_id, seat)` rows until those rows are cleaned.
- Ornn recycle-order assertions intentionally lock current deterministic order and must be revisited if random/recycle semantics intentionally change.
- This bundle does not close P0/P1, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.
