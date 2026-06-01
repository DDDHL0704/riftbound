# Stage 4D-17OZ Recovery Timing Continuous Effect Sequence Canonicality Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks the local sequence invariant emitted by the snapshot builder. Continuous-effect `sequence` values must be positive, unique in the same payload and contiguous from `1`. The spectator coverage includes a continuous-effect count mismatch case so same-payload sequence diagnostics still run before authoritative parity is skipped.

## Validation

- Focused sequence canonicality tests: `2/2`
- Focused recovery tests: `440/440`
- Adjacent recovery/opening/store-smoke tests: `1021/1021`
- Backend full: `6386/6386`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
