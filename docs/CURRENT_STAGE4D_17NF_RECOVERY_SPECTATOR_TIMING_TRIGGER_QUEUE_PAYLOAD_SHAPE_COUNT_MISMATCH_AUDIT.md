# Stage 4D-17NF Recovery Spectator Timing Trigger Queue Payload Shape Count Mismatch Audit

Date: 2026-05-28 04:30 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates list-shaped `Timing["triggerQueue"][]` item payload shape, property names, scalar values and duplicate trigger ids even when the spectator trigger-queue count already differs from authoritative state.

Runtime change: spectator trigger-queue validation now separates same-payload item validation from authoritative indexed parity. Count mismatches still produce the explicit authoritative count diagnostic, but they no longer stop malformed spectator trigger-queue item diagnostics from being accumulated. Indexed authoritative parity still runs only when spectator and authoritative counts match.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueuePayloadShapeWithCountMismatch` proves an extra malformed spectator trigger-queue entry emits the explicit trigger queue item payload required diagnostic alongside the authoritative trigger-queue count mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `389/389`
- Adjacent recovery/opening/store-smoke: `970/970`
- Backend full: `6335/6335`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NF_RECOVERY_SPECTATOR_TIMING_TRIGGER_QUEUE_PAYLOAD_SHAPE_COUNT_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
