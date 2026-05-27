# Stage 4D-17NV Recovery Spectator Snapshot Player Extra Object Location Parity Visibility Mismatch Audit

Date: 2026-05-28 06:45 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates authoritative location parity for object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that resolve to authoritative object locations and are not visible in the authoritative spectator view.

Runtime change: spectator snapshot player extra object location validation now compares present valid nested `location.playerId`, `location.zone` and `location.battlefieldObjectId` values against authoritative object location data after payload/property/value-shape validation succeeds. Extra object-shaped entries outside the authoritative visible object-id set now emit explicit location parity mismatch diagnostics alongside the existing authoritative spectator visibility mismatch when a hidden object is surfaced with a wrong but well-shaped location.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationParityWithVisibilityMismatch` proves an extra hidden hand object payload with a valid but wrong battlefield location emits location player id, zone and battlefield object id mismatch diagnostics alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `405/405`
- Adjacent recovery/opening/store-smoke: `986/986`
- Backend full: `6351/6351`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NV_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_LOCATION_PARITY_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
