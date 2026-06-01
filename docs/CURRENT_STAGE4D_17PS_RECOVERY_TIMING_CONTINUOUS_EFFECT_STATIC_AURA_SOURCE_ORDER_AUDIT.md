# Stage 4D-17PS Recovery Timing Continuous Effect Static Aura Source Order Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks that `STATIC_AURA` effects include a non-null `sourceOrder`. This matches current builder output because static-aura effects are derived from public-field source objects and `BuildContinuousEffectStates` applies public-field source ordering before snapshot serialization. Other continuous-effect layers keep existing optional source-order compatibility. Malformed, zero or negative source-order values keep the existing dedicated optional-positive-int diagnostics from earlier slices. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura source-order diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura source-order consistency tests: `3/3`
- Focused recovery tests: `478/478`
- Adjacent recovery/opening/store-smoke tests: `1059/1059`
- Backend full: `6424/6424`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
