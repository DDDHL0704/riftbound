# Stage 4D-17PV Recovery Timing Continuous Effect Static Aura Participant Dependency Object Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects `STATIC_AURA` effects whose readable non-empty `participantDependencyObjectIds` list omits any readable `participantObjectIds` entry. This matches current builder output because static-aura participant dependency lists are derived from participant object ids and public-field dependency expansion. Missing/null/empty participant dependency lists keep existing optional compatibility, malformed lists keep existing string-list diagnostics, and the battlefield participant-target validation from prior slices remains unchanged. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura participant-dependency diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura participant-dependency-object consistency tests: `2/2`
- Focused recovery tests: `484/484`
- Adjacent recovery/opening/store-smoke tests: `1065/1065`
- Backend full: `6430/6430`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
