# Stage 4D-17PT Recovery Timing Continuous Effect Static Aura Applied Order Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects `STATIC_AURA` effects carrying a readable non-null integer `appliedOrder`. This matches current builder output because `AppliedOrder` is only populated from temporary power modifier ledger entries, while current static-aura builders never set it and snapshot serialization only writes `appliedOrder` when it has a value. Absent/null `appliedOrder` keeps existing optional-field compatibility, and malformed `appliedOrder` values keep the existing optional-int diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura applied-order diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura applied-order consistency tests: `2/2`
- Focused recovery tests: `480/480`
- Adjacent recovery/opening/store-smoke tests: `1061/1061`
- Backend full: `6426/6426`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
