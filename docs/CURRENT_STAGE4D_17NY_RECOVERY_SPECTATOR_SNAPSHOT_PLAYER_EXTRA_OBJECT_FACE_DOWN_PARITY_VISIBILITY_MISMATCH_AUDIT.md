# Stage 4D-17NY Recovery Spectator Snapshot Player Extra Object Face-Down Parity Visibility Mismatch Audit

Date: 2026-05-28 07:11 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates valid `isFaceDown` values on known object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that are not expected for that snapshot player but resolve to authoritative card objects.

Runtime change: spectator snapshot player extra object redaction now computes expected face-down from authoritative card-object face-down state plus spectator-hidden locations. Known extra objects emit the existing face-down redaction mismatch diagnostic when payload `isFaceDown` disagrees with that expected value, covering both hidden/face-down objects that self-declare face-up and visible face-up objects that self-declare face-down. Expected or self-declared face-down payloads that expose private metadata continue to emit the hidden face-down metadata diagnostic.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraVisibleObjectFaceDownParityWithVisibilityMismatch` proves Bob's visible face-up battlefield object copied into Alice's `objects{}` with `isFaceDown: true` emits the face-down parity mismatch diagnostic alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `408/408`
- Adjacent recovery/opening/store-smoke: `989/989`
- Backend full: `6354/6354`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NY_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_FACE_DOWN_PARITY_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
