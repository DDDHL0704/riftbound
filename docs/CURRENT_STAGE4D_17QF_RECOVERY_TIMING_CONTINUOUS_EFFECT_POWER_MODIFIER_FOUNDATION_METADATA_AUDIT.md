# Stage 4D-17QF Recovery Timing Continuous Effect Power Modifier Foundation Metadata Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `POWER_MODIFIER` effects carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals when `effectKind` or `sourcePath` is missing/null. Current builders only emit foundation-only power modifiers from tracked ledger entries or legacy remainder, and both carry non-empty effect kind and source path. Simple legacy power modifiers carry neither foundation-only status nor deferred residuals. Malformed/empty optional string/list payloads keep their existing diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload foundation metadata diagnostics still run before authoritative parity is skipped.

## Validation

- Focused power-modifier foundation metadata tests: `2/2`
- Focused recovery tests: `504/504`
- Adjacent recovery/opening/store-smoke tests: `1085/1085`
- Backend full: `6450/6450`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
