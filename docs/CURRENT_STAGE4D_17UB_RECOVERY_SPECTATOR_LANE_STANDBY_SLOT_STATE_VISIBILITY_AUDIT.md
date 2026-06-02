# Stage 4D-17UB Recovery Spectator Lane Standby Slot State Visibility Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot lane standby-slot payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that spectator replay-frame snapshot lane `battlefields[].standbySlots[]` payloads validated `visible` and `state` against authoritative spectator state, but did not validate that the two values were internally consistent with each other. Recovered player-view lane standby slots already reject `visible=false` with `state=VISIBLE`; spectator replay-frame payloads now do the same before authoritative parity diagnostics continue.

## Runtime Change

`MatchRecoveryValidator` now emits a same-payload diagnostic when a readable spectator standby slot `state` does not match the same payload's readable `visible` flag.

Expected mapping:

- `visible=true` requires `state=VISIBLE`.
- `visible=false` requires `state=HIDDEN`.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneStandbySlotStateVisibilityConsistencyWithCountMismatch`.

The test mutates a spectator replay-frame snapshot lane with:

- `battlefieldCount = 3` while authoritative battlefield object count is `2`;
- a visible authoritative standby slot payload changed to `visible=false`;
- the same standby slot payload left at `state=VISIBLE`.

Expected diagnostics are:

- explicit same-payload `state does not match visibility`;
- authoritative spectator visibility mismatch;
- lane battlefield count mismatch.

## Validation

- Focused state/visibility consistency test: `1/1`.
- Focused `SpectatorReplaySnapshotLane` filter: `14/14`.
- Focused `SpectatorReplaySnapshotStandbySlot` filter: `3/3`.
- Focused `SpectatorReplaySnapshotBattlefield` filter: `6/6`.
- Focused `MatchRecoveryTests` filter: `655/655`.
- Adjacent recovery/opening/store-smoke filter: `1236/1236`.
- Backend full: `6601/6601`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
