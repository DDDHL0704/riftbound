2026-06-01 Stage 4D-17SF recovery timing battlefield-task object-reference registry validation audit

Status: accepted for this narrow A_MAIN runtime/server closure slice. Project remains **NOT READY**.

Scope:
- Runtime/test files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Documentation changed: current checkpoint/completion/P0-P1/next-dispatch docs, shared coordination board, and this audit.
- Locked/out of scope: matrix JSON semantics, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and solution/IDE files.

Runtime closure:
- `MatchRecoveryValidator` now validates recovered player-view snapshot timing `battlefieldTasks[]` object references against snapshot `players.*.objects{}` registry keys.
- `MatchRecoveryValidator` now validates spectator replay-frame timing `battlefieldTasks[]` object references against authoritative `CardObjects` / `ObjectLocations` registry keys.
- Covered object-reference fields are `battlefieldObjectId` and `participantObjectIds`.
- Same-payload object-reference diagnostics still run when spectator battlefield-task count parity is already mismatched and authoritative battlefield-task parity comparison is skipped.

New tests:
- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskObjectReferencesOutsideSnapshotObjects`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskObjectReferencesOutsideRegistry`

Validation:
- Focused new object-reference tests: `2/2`.
- Focused battlefield-task filter: `26/26`.
- Focused recovery: `586/586`.
- Adjacent recovery/opening/store-smoke filter: `1167/1167`.
- Backend full: `6532/6532`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs src tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Residual risk:
- This slice narrows P1-004 recovery/replay determinism for battlefield-task object-reference membership only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
