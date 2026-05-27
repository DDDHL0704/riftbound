# Stage 4D-17NG Recovery Spectator Timing Resolution History Payload Shape Count Mismatch Audit

Date: 2026-05-28 04:39 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates list-shaped `Timing["battlefieldResolutions"][]` and `Timing["battleResolutions"][]` item payload shape, property names, scalar values, list values and duplicate resolution ids even when the spectator resolution-history counts already differ from authoritative state.

Runtime change: spectator resolution-history validation now separates same-payload item validation from authoritative indexed parity. Count mismatches still produce the explicit authoritative battlefield/battle resolution count diagnostics, but they no longer stop malformed spectator resolution item diagnostics from being accumulated. Indexed authoritative parity still runs only when spectator and authoritative counts match.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryPayloadShapeWithCountMismatch` proves extra malformed spectator battlefield and battle resolution entries emit the explicit resolution payload required diagnostics alongside the authoritative resolution-count mismatch diagnostics.

Validation:

- Focused single: `1/1`
- Focused recovery: `390/390`
- Adjacent recovery/opening/store-smoke: `971/971`
- Backend full: `6336/6336`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NG_RECOVERY_SPECTATOR_TIMING_RESOLUTION_HISTORY_PAYLOAD_SHAPE_COUNT_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
