# Stage 4D-17NT Recovery Spectator Snapshot Player Extra Object Redaction Visibility Mismatch Audit

Date: 2026-05-28 06:27 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame snapshot player object recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates hidden face-down private metadata redaction for object-shaped entries in `SpectatorSnapshot.Players[playerId].objects{}` that are not visible in the authoritative spectator view.

Runtime change: spectator snapshot player object validation now emits the hidden face-down private-metadata diagnostic for extra object-shaped `objects{}` entries outside the authoritative visible object-id set when they self-declare `isFaceDown: true` while exposing `cardNo`, `tags` or `power`. Existing visible-object redaction parity still runs only over the authoritative visible object set.

Test coverage: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectRedactionWithVisibilityMismatch` proves an extra hidden hand object payload that exposes `cardNo`, `tags` and `power` emits the hidden face-down private-metadata diagnostic alongside the authoritative spectator visibility mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `403/403`
- Adjacent recovery/opening/store-smoke: `984/984`
- Backend full: `6349/6349`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NT_RECOVERY_SPECTATOR_SNAPSHOT_PLAYER_EXTRA_OBJECT_REDACTION_VISIBILITY_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
