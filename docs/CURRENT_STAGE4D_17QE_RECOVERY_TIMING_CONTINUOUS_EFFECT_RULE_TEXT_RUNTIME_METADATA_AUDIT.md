# Stage 4D-17QE Recovery Timing Continuous Effect Rule Text Runtime Metadata Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `RULE_TEXT` effects carrying readable non-empty `effectKind`, `sourceCardNo`, `sourcePath`, `layerEngineStatus` or `deferredLayerEngineResiduals` values. Current continuous-effect builders emit rule-text effects as rule payloads only, and global/object rule-text effects do not carry runtime effect metadata, source metadata, foundation-only LayerEngine status or deferred LayerEngine residuals. Malformed optional string/list payloads keep their existing optional-string/list diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload rule-text runtime metadata diagnostics still run before authoritative parity is skipped.

## Validation

- Focused rule-text runtime metadata absence tests: `2/2`
- Focused recovery tests: `502/502`
- Adjacent recovery/opening/store-smoke tests: `1083/1083`
- Backend full: `6448/6448`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
