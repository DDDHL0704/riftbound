# Stage 4D-17OW Recovery Timing Trigger Queue Source Redaction Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in trigger-queue timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `triggerQueue[]` item validation now checks the local source-redaction invariant emitted by the snapshot builder. When `sourceVisibility` is `HIDDEN`, `sourceObjectId` and `effectKind` must both be the `HIDDEN` redaction sentinel. When `sourceVisibility` is `VISIBLE`, those fields must not remain redacted as `HIDDEN`. The spectator coverage includes a trigger-queue count mismatch case so same-payload redaction diagnostics still run before authoritative parity is skipped.

## Validation

- Focused redaction consistency tests: `2/2`
- Focused recovery tests: `434/434`
- Adjacent recovery/opening/store-smoke tests: `1015/1015`
- Backend full: `6380/6380`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
