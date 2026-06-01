2026-06-01 Stage 4D-17SE recovery timing continuous-effect object-reference registry validation audit

Status: accepted for this narrow A_MAIN runtime/server closure slice. Project remains **NOT READY**.

Scope:
- Runtime/test files changed: `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Documentation changed: current checkpoint/completion/P0-P1/next-dispatch docs, shared coordination board, and this audit.
- Locked/out of scope: matrix JSON semantics, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and solution/IDE files.

Runtime closure:
- `MatchRecoveryValidator` now validates recovered player-view snapshot timing `continuousEffects[]` object references against snapshot `players.*.objects{}` registry keys.
- `MatchRecoveryValidator` now validates spectator replay-frame timing `continuousEffects[]` object references against authoritative `CardObjects` / `ObjectLocations` registry keys.
- Covered object-reference fields are `targetObjectId`, `sourceObjectId`, `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds` and `participantDependencyObjectIds`.
- Same-payload object-reference diagnostics still run when spectator continuous-effect count parity is already mismatched and authoritative continuous-effect parity comparison is skipped.

New tests:
- `RecoveryValidatorRejectsSnapshotTimingContinuousEffectObjectReferencesOutsideSnapshotObjects`
- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectObjectReferencesOutsideRegistry`

Validation:
- Focused new object-reference tests: `2/2`.
- Focused continuous-effect filter: `129/129`.
- Focused recovery: `584/584`.
- Adjacent recovery/opening/store-smoke filter: `1165/1165`.
- Backend full: `6530/6530`.
- Mechanical checks: `git diff --check`; anchored conflict-marker scan over `docs src tests`; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Residual risk:
- This slice narrows P1-004 recovery/replay determinism for continuous-effect object-reference membership only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
