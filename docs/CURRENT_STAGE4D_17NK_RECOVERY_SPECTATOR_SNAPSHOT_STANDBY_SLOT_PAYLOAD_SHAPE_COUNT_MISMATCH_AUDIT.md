# Stage 4D-17NK Recovery Spectator Snapshot Standby Slot Payload Shape Count Mismatch Audit

Date: 2026-05-28 05:13 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot lane recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates extra list-shaped `SpectatorSnapshot.Lanes["battlefields"][].standbySlots[]` item payload shape when spectator standby-slot entries outnumber authoritative standby slots.

Runtime change: spectator snapshot lane battlefield standby-slot validation now emits an explicit extra standby-slot item payload-shape diagnostic for non-object `standbySlots[]` entries beyond the authoritative standby-slot count. Existing authoritative indexed standby-slot parity still runs over the established authoritative range.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotStandbySlotPayloadShapeWithCountMismatch` proves an extra malformed spectator standby-slot entry emits the explicit standby-slot item payload required diagnostic alongside the authoritative standby-slot count mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `394/394`
- Adjacent recovery/opening/store-smoke: `975/975`
- Backend full: `6340/6340`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NK_RECOVERY_SPECTATOR_SNAPSHOT_STANDBY_SLOT_PAYLOAD_SHAPE_COUNT_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
