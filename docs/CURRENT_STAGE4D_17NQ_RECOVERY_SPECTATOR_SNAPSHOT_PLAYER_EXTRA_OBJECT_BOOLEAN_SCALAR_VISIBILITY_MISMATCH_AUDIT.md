# Stage 4D-17NQ Recovery Spectator Snapshot Player Extra Object Boolean Scalar Visibility Mismatch Audit

Date: 2026-05-28 06:02 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates optional boolean scalars for object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that are not visible in the authoritative spectator view.

Runtime change: spectator snapshot player object validation now emits explicit `isExhausted`, `isAttacking` and `isDefending` boolean-scalar value-shape diagnostics for extra object-shaped `objects{}` entries outside the authoritative visible object-id set. Existing visible-object location, redaction, string, numeric and list parity still runs over the authoritative visible object set.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectBooleanScalarValueShapeWithVisibilityMismatch` proves an extra hidden hand object payload with malformed exhausted, attacking and defending states emits explicit player object boolean-scalar diagnostics alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `400/400`
- Adjacent recovery/opening/store-smoke: `981/981`
- Backend full: `6346/6346`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NQ_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_BOOLEAN_SCALAR_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
