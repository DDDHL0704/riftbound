# Stage 4D-17PR Recovery Timing Continuous Effect Static Aura Participant Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks battlefield-scope `STATIC_AURA` effects with duration `WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD`. Those payloads must include a non-null `participantObjectIds` list, and a readable non-empty participant list must include the readable `targetObjectId`. Object-scope static auras keep existing participant-list optional compatibility; malformed and empty participant lists keep the existing dedicated payload-shape/value/non-empty diagnostics from earlier slices. The spectator coverage includes a continuous-effect count mismatch case so same-payload battlefield static-aura participant diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura participant consistency tests: `3/3`
- Focused recovery tests: `476/476`
- Adjacent recovery/opening/store-smoke tests: `1057/1057`
- Backend full: `6422/6422`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
