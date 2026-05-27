# Stage 4D-17NJ Recovery Spectator Snapshot Lane Battlefield Payload Shape Count Mismatch Audit

Date: 2026-05-28 05:04 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot lane recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates extra list-shaped `SpectatorSnapshot.Lanes["battlefields"][]` item payload shape when spectator lane battlefield entries outnumber authoritative battlefield state.

Runtime change: spectator snapshot lane battlefield validation now emits an explicit extra battlefield item payload-shape diagnostic for non-object `battlefields[]` entries beyond the authoritative battlefield count. Existing authoritative indexed property/value/list/standby parity still runs over the established authoritative range.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldPayloadShapeWithCountMismatch` proves an extra malformed spectator lane battlefield entry emits the explicit battlefield item payload required diagnostic alongside the authoritative battlefield count mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `393/393`
- Adjacent recovery/opening/store-smoke: `974/974`
- Backend full: `6339/6339`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NJ_RECOVERY_SPECTATOR_SNAPSHOT_LANE_BATTLEFIELD_PAYLOAD_SHAPE_COUNT_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
