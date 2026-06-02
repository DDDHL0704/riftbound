# Stage 4D-17UA Recovery Spectator Lane Standby Slot State Known-Value Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot lane standby-slot payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that spectator replay-frame snapshot lane `battlefields[].standbySlots[]` `state` values were compared against authoritative spectator state, but unlike recovered player-view lane standby slots, they were not first validated as known standby-slot states. A forged `UNKNOWN` value under lane battlefield count mismatch therefore produced only broad authoritative state mismatch diagnostics.

## Runtime Change

`MatchRecoveryValidator` now passes the existing `IsKnownStandbySlotState` predicate into spectator standby-slot `state` validation. Valid values remain `VISIBLE` and `HIDDEN`.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneStandbySlotStateKnownValueWithCountMismatch`.

The test mutates a spectator replay-frame snapshot lane with:

- `battlefieldCount = 3` while authoritative battlefield object count is `2`;
- a visible standby slot `state = "UNKNOWN"`.

Expected diagnostics are:

- explicit invalid-state diagnostic for `UNKNOWN`;
- authoritative spectator-state mismatch diagnostic;
- lane battlefield count mismatch diagnostic.

## Validation

- Focused standby-state known-value test: `1/1`.
- Focused `SpectatorReplaySnapshotLane` filter: `13/13`.
- Focused `SpectatorReplaySnapshotStandbySlot` filter: `3/3`.
- Focused `SpectatorReplaySnapshotBattlefield` filter: `6/6`.
- Focused `MatchRecoveryTests` filter: `654/654`.
- Adjacent recovery/opening/store-smoke filter: `1235/1235`.
- Backend full: `6600/6600`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
