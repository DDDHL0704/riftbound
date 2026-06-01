# Stage 4D-17PW Recovery Timing Continuous Effect Static Aura Participant Dependency List Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects `STATIC_AURA` effects whose readable non-empty `participantObjectIds` list lacks a present non-null `participantDependencyObjectIds` payload. This matches current builder output because static-aura participant object lists and participant dependency object lists are emitted together when participants exist, while static auras with no participant list keep existing optional compatibility. Malformed participant lists and malformed/empty participant dependency lists keep their existing dedicated diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura participant-dependency-list diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura participant-dependency-list consistency tests: `2/2`
- Focused recovery tests: `486/486`
- Adjacent recovery/opening/store-smoke tests: `1067/1067`
- Backend full: `6432/6432`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
