# Stage 4D-17OB Recovery Spectator Snapshot Visible Object Self-Declared Face-Down Private Metadata Audit

Date: 2026-05-28

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in spectator replay-frame snapshot player object redaction.

The runtime change is limited to `MatchRecoveryValidator.ValidateSpectatorReplayFrame`: expected visible spectator player object payloads that self-declare `isFaceDown:true` against authoritative face-up state now also run face-down private-metadata checks when normal visible object fields remain exposed. This aligns the expected visible-object path with the extra-object redaction behavior from 17OA while preserving the face-down flag mismatch diagnostic.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `411/411`
- Adjacent recovery/opening/store-smoke tests: `992/992`
- Backend full: `6357/6357`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
