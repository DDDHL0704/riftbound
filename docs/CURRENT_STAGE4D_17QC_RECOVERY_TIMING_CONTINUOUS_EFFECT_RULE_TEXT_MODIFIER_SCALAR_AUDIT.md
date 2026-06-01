# Stage 4D-17QC Recovery Timing Continuous Effect Rule Text Modifier Scalar Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `RULE_TEXT` effects carrying readable non-null `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower`, `appliedOrder` or `sourceOrder` values. Current continuous-effect builders emit rule-text effects as rule payloads only: global and object rule-text effects never carry these temporary modifier/order scalars. Malformed optional integer payloads keep their existing optional-int diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload rule-text modifier/order-scalar diagnostics still run before authoritative parity is skipped.

## Validation

- Focused rule-text modifier-scalar absence tests: `2/2`
- Focused recovery tests: `498/498`
- Adjacent recovery/opening/store-smoke tests: `1079/1079`
- Backend full: `6444/6444`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
