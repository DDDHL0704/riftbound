# Stage 4D-17UC Recovery Spectator Lane Standby Slot Hidden Object Redaction Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot lane standby-slot payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that spectator replay-frame snapshot lane `battlefields[].standbySlots[]` payloads redacted `objectId` only when authoritative spectator visibility expected the slot to be hidden. If the same payload claimed `visible=false` while authoritative state still expected the standby slot to be visible, the validator emitted broad authoritative visibility/state drift but did not explicitly identify the same-payload redaction violation. Recovered player-view lane standby slots already reject hidden payloads carrying `objectId`; spectator replay-frame payloads now do the same.

## Runtime Change

`MatchRecoveryValidator` now emits a same-payload diagnostic when a readable spectator standby slot has `visible=false` and still carries `objectId`.

The new diagnostic runs before authoritative visible object-id validation. The existing authoritative-hidden redaction branch remains in place and is guarded to avoid duplicate hidden-object diagnostics when both rules observe the same payload.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneStandbySlotHiddenObjectRedactionWithCountMismatch`.

The test mutates a spectator replay-frame snapshot lane with:

- `battlefieldCount = 3` while authoritative battlefield object count is `2`;
- a visible authoritative standby slot payload changed to `visible=false`;
- the same standby slot payload changed to `state=HIDDEN`;
- the original visible `objectId` retained.

Expected diagnostics are:

- explicit same-payload `hidden object id must be redacted`;
- authoritative spectator visibility mismatch;
- authoritative spectator state mismatch;
- lane battlefield count mismatch.

## Validation

- Focused hidden object redaction test: `1/1`.
- Focused `SpectatorReplaySnapshotLane` filter: `15/15`.
- Focused `SpectatorReplaySnapshotStandbySlot` filter: `3/3`.
- Focused `SpectatorReplaySnapshotBattlefield` filter: `6/6`.
- Focused `MatchRecoveryTests` filter: `656/656`.
- Adjacent recovery/opening/store-smoke filter: `1237/1237`.
- Backend full: `6602/6602`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
