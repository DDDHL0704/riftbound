# Stage 4D-17PI Recovery Timing Continuous Effect Duration Known Value Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects unknown `duration` values before downstream continuous-effect consumers and parity-only checks run. Accepted current builder durations are `UNTIL_END_OF_TURN`, `WHILE_SOURCE_ON_PUBLIC_FIELD` and `WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD`. This aligns duration validation with the existing known-value checks for `scope`, `layer` and `layerEngineStatus`. The spectator coverage includes a continuous-effect count mismatch case so same-payload unknown-duration diagnostics still run before authoritative parity is skipped.

## Validation

- Focused duration known-value tests: `2/2`
- Focused recovery tests: `458/458`
- Adjacent recovery/opening/store-smoke tests: `1039/1039`
- Backend full: `6404/6404`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
