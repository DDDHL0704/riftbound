# Stage 4D-17NR Recovery Spectator Snapshot Player Extra Object List Scalar Visibility Mismatch Audit

Date: 2026-05-28 06:10 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates optional string-list scalars for object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that are not visible in the authoritative spectator view.

Runtime change: spectator snapshot player object validation now emits explicit `tags` and `untilEndOfTurnEffects` list-scalar payload/value diagnostics for extra object-shaped `objects{}` entries outside the authoritative visible object-id set. Existing visible-object location, redaction, string, numeric and boolean parity still runs over the authoritative visible object set.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectListScalarValueShapeWithVisibilityMismatch` proves an extra hidden hand object payload with malformed tag payload and malformed until-end-of-turn effect entries emits explicit player object list-scalar diagnostics alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `401/401`
- Adjacent recovery/opening/store-smoke: `982/982`
- Backend full: `6347/6347`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NR_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_LIST_SCALAR_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
