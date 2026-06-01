# Stage 4D-17QB Recovery Timing Continuous Effect Rule Text Power Scalar Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `RULE_TEXT` effects carrying nonzero `powerDelta` values, and global `RULE_TEXT` effects carrying nonzero `basePower` or `effectivePower` values. Current continuous-effect builders emit rule-text effects as rule payloads only: object rule-text effects keep `powerDelta` at `0`, and global rule-text effects keep `powerDelta`, `basePower` and `effectivePower` at `0`. Malformed required integer payloads keep their existing required-int diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload rule-text power-scalar diagnostics still run before authoritative parity is skipped.

## Validation

- Focused rule-text power-scalar consistency tests: `2/2`
- Focused recovery tests: `496/496`
- Adjacent recovery/opening/store-smoke tests: `1077/1077`
- Backend full: `6442/6442`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
