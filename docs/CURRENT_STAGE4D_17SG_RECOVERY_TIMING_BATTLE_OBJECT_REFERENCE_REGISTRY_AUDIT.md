2026-06-01 Stage 4D-17SG recovery timing battle object-reference registry validation audit

Status: accepted for this narrow A_MAIN runtime/server closure slice. Project remains **NOT READY**.

Scope:
- Runtime/test files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Documentation changed: current checkpoint/completion/P0-P1/next-dispatch docs, shared coordination board, and this audit.
- Locked/out of scope: matrix JSON semantics, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and solution/IDE files.

Runtime closure:
- `MatchRecoveryValidator` now validates recovered player-view snapshot timing `battle` object references against snapshot `players.*.objects{}` registry keys.
- `MatchRecoveryValidator` now validates spectator replay-frame timing `battle` object references against authoritative `CardObjects` / `ObjectLocations` registry keys.
- Covered object-reference fields are `battlefieldObjectId`, `attackerObjectIds`, `defenderObjectIds` and `participantControllerIds` object-id keys.
- Same-payload object-reference diagnostics run before spectator battle authoritative-parity drift is reported.

New tests:
- `RecoveryValidatorRejectsSnapshotTimingBattleObjectReferencesOutsideSnapshotObjects`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleObjectReferencesOutsideRegistry`

Validation:
- Focused new object-reference tests: `2/2`.
- Focused Battle filter: `673/673`.
- Focused recovery: `588/588`.
- Adjacent recovery/opening/store-smoke filter: `1169/1169`.
- Backend full: `6534/6534`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs src tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Residual risk:
- This slice narrows P1-004 recovery/replay determinism for timing battle object-reference membership only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
