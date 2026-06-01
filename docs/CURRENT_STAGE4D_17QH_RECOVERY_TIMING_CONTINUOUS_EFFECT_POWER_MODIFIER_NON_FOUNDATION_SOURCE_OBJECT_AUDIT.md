# Stage 4D-17QH Recovery Timing Continuous Effect Power Modifier Non-Foundation Source Object Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `POWER_MODIFIER` effects carrying a non-null `sourceObjectId` without foundation-only LayerEngine status and without readable deferred LayerEngine residuals. Current continuous-effect builders emit non-foundation power modifiers only for simple legacy power deltas, and that path carries no source object. Tracked ledger entries and legacy remainders carry foundation-only status plus residuals before source metadata is allowed. Malformed/empty nullable source-object payloads keep their existing diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload non-foundation source-object diagnostics still run before authoritative parity is skipped.

## Validation

- Focused power-modifier non-foundation source-object tests: `2/2`
- Focused recovery tests: `508/508`
- Adjacent recovery/opening/store-smoke tests: `1089/1089`
- Backend full: `6454/6454`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
