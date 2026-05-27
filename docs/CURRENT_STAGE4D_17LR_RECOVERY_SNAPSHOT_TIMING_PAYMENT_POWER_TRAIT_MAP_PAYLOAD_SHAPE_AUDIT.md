# Stage 4D-17LR Recovery Snapshot Timing Payment Power Trait Map Payload Shape Audit

Date: 2026-05-27 22:59 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot timing payment power trait map payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now distinguishes present malformed payment power trait map payloads from map-shaped payloads.
- Present non-map `Timing["pendingPayment"]["cost"]["powerByTrait"]` now produces an explicit pending payment cost map payload-shape diagnostic.
- Present non-map `Timing["temporaryPaymentResources"][]["generatedPowerByTrait"]` and `Timing["temporaryPaymentResources"][]["remainingPowerByTrait"]` now produce explicit temporary payment resource map payload-shape diagnostics.
- Missing/null maps keep existing required diagnostics.
- Valid map-shaped payloads continue through existing positive integer value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotTimingPaymentPowerTraitMapPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot pending payment and temporary payment resource power trait maps to non-map payloads and asserts explicit map payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `349/349`.
- Adjacent recovery/opening/store-smoke filter: `930/930`.
- Backend full suite: `6295/6295`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
