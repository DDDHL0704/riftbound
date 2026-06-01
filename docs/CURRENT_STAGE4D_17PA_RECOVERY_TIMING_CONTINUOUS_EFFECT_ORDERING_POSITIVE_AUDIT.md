# Stage 4D-17PA Recovery Timing Continuous Effect Ordering Positive Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now enforces the snapshot builder's optional ordering scalar canonicality. Optional `appliedOrder` and `sourceOrder` values must be positive when present. The spectator coverage includes a continuous-effect count mismatch case so same-payload ordering diagnostics still run before authoritative parity is skipped.

## Validation

- Focused ordering positive tests: `2/2`
- Focused recovery tests: `442/442`
- Adjacent recovery/opening/store-smoke tests: `1023/1023`
- Backend full: `6388/6388`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
