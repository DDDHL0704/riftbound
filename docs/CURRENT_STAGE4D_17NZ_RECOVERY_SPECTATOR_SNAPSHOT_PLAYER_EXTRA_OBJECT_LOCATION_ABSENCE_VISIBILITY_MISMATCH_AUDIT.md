# Stage 4D-17NZ Recovery Spectator Snapshot Player Extra Object Location Absence Visibility Mismatch Audit

Date: 2026-05-28 07:19 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects present nested location payloads on known object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` when those object ids resolve to authoritative card objects but have no authoritative object location.

Runtime change: spectator snapshot player extra object location validation still validates present nested location payload shape and values under visibility drift, but now emits `location must be absent without authoritative object location` when `ExpectedSpectatorObjectLocation` is null for a known authoritative card object. Unknown extra object ids without authoritative locations keep the prior compatibility path.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationAbsenceWithVisibilityMismatch` proves a known orphan card object copied into Alice's `objects{}` with a fabricated battlefield location emits the location-absence diagnostic alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `409/409`
- Adjacent recovery/opening/store-smoke: `990/990`
- Backend full: `6355/6355`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NZ_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_LOCATION_ABSENCE_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
