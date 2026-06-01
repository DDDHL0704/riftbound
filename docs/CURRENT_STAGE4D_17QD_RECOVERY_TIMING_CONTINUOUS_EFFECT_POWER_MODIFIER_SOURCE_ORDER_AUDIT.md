# Stage 4D-17QD Recovery Timing Continuous Effect Power Modifier Source Order Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `POWER_MODIFIER` effects carrying a readable `sourceOrder` value when `sourceObjectId` is null. Current continuous-effect builders apply `sourceOrder` only after `SourceObjectId` is non-empty and maps to a public-field source order, so power-modifier entries without a source object cannot canonically carry source ordering. Malformed optional integer payloads keep their existing optional-int diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload power-modifier source-order diagnostics still run before authoritative parity is skipped.

## Validation

- Focused power-modifier source-order consistency tests: `2/2`
- Focused recovery tests: `500/500`
- Adjacent recovery/opening/store-smoke tests: `1081/1081`
- Backend full: `6446/6446`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
