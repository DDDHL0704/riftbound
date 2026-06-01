# Stage 4D-17QG Recovery Timing Continuous Effect Power Modifier Non-Foundation Metadata Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known valid-scope `POWER_MODIFIER` effects carrying readable non-empty `effectKind`, `sourceCardNo` or `sourcePath` values without foundation-only LayerEngine status and without readable deferred LayerEngine residuals. Current continuous-effect builders emit non-foundation power modifiers only for simple legacy power deltas, and that path carries no runtime/source metadata. Tracked ledger entries and legacy remainders carry foundation-only status plus residuals before runtime/source metadata is allowed. Malformed/empty optional string/list payloads keep their existing diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload non-foundation metadata diagnostics still run before authoritative parity is skipped.

## Validation

- Focused power-modifier non-foundation metadata tests: `2/2`
- Focused recovery tests: `506/506`
- Adjacent recovery/opening/store-smoke tests: `1087/1087`
- Backend full: `6452/6452`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
