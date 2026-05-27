# Stage 4D-17NH Recovery Spectator Timing Temporary Payment Resource Payload Shape Count Mismatch Audit

Date: 2026-05-28 04:47 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates list-shaped `Timing["temporaryPaymentResources"][]` item payload shape, property names, scalar values, map values, list values and duplicate resource ids even when the spectator temporary-payment-resource count already differs from authoritative state.

Runtime change: spectator temporary-payment-resource validation now separates same-payload item validation from authoritative indexed parity. Count mismatches still produce the explicit authoritative temporary-payment-resource count diagnostic, but they no longer stop malformed spectator temporary-payment-resource item diagnostics from being accumulated. Indexed authoritative parity still runs only when spectator and authoritative counts match.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourcePayloadShapeWithCountMismatch` proves an extra malformed spectator temporary-payment-resource entry emits the explicit temporary-payment-resource payload required diagnostic alongside the authoritative temporary-payment-resource count mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `391/391`
- Adjacent recovery/opening/store-smoke: `972/972`
- Backend full: `6337/6337`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NH_RECOVERY_SPECTATOR_TIMING_TEMPORARY_PAYMENT_RESOURCE_PAYLOAD_SHAPE_COUNT_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
