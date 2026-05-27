# Stage 4D-17NW Recovery Spectator Snapshot Player Extra Object Location Required Visibility Mismatch Audit

Date: 2026-05-28 06:53 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now requires a nested location payload for object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that resolve to authoritative object locations and are not visible in the authoritative spectator view.

Runtime change: spectator snapshot player extra object location validation now computes authoritative object location before checking nested `location`. Extra object-shaped entries outside the authoritative visible object-id set emit the existing `location is required` diagnostic when their object ids resolve to authoritative locations and the payload omits or nulls `location`; unknown extra object ids without authoritative locations keep the previous compatibility path.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationRequiredWithVisibilityMismatch` proves an extra hidden hand object payload without nested location emits the location required diagnostic alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `406/406`
- Adjacent recovery/opening/store-smoke: `987/987`
- Backend full: `6352/6352`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NW_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_LOCATION_REQUIRED_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
