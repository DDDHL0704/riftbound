# Stage 4D-17NX Recovery Spectator Snapshot Player Extra Visible Object Scalar Parity Visibility Mismatch Audit

Date: 2026-05-28 07:03 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now applies visible-object scalar parity checks to object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that are not expected for that snapshot player but resolve to authoritative visible card objects.

Runtime change: spectator snapshot player extra object validation now detects known authoritative card objects outside the current player's expected visible object-id set and, when the object is not hidden and not face-down, reuses the visible-object scalar parity checks for string, numeric, boolean and list scalar fields. Hidden hand/main/rune/hidden-standby objects and face-down objects remain excluded from visible scalar parity to preserve redaction semantics.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraVisibleObjectScalarParityWithVisibilityMismatch` proves Bob's visible battlefield object copied into Alice's `objects{}` with drifted card number, owner/controller ids, attachment id, numeric state, boolean state, tags and until-end-of-turn effects emits explicit authoritative scalar mismatch diagnostics alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `407/407`
- Adjacent recovery/opening/store-smoke: `988/988`
- Backend full: `6353/6353`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NX_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_VISIBLE_OBJECT_SCALAR_PARITY_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
