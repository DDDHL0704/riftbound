# Stage 4D-17NU Recovery Spectator Snapshot Player Extra Object Face-Down Redaction Visibility Mismatch Audit

Date: 2026-05-28 06:35 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates authoritative face-down redaction for object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that resolve to spectator-hidden object ids and are not visible in the authoritative spectator view.

Runtime change: spectator snapshot player object validation now emits the face-down flag spectator-redaction mismatch diagnostic for extra object-shaped `objects{}` entries outside the authoritative visible object-id set when those ids resolve to hidden hand, main-deck, rune-deck or hidden-standby objects and the payload self-declares `isFaceDown: false`. Known hidden extra objects that expose `cardNo`, `tags` or `power` also continue to emit the private-metadata diagnostic.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectFaceDownRedactionWithVisibilityMismatch` proves an extra hidden hand object payload with false face-down state emits the face-down flag spectator-redaction mismatch diagnostic alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `404/404`
- Adjacent recovery/opening/store-smoke: `985/985`
- Backend full: `6350/6350`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NU_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_FACE_DOWN_REDACTION_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
