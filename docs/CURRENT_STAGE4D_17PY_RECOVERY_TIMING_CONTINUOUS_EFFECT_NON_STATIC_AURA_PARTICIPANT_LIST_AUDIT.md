# Stage 4D-17PY Recovery Timing Continuous Effect Non Static Aura Participant List Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known non-`STATIC_AURA` layers carrying readable non-empty `participantObjectIds` or `participantDependencyObjectIds` lists. Current continuous-effect builders reserve participant and participant-dependency lists for static-aura effects; temporary `POWER_MODIFIER` and `RULE_TEXT` effects should not carry those static-aura participant payloads. Malformed participant lists and empty participant lists keep their existing dedicated diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload non-static-aura participant-list diagnostics still run before authoritative parity is skipped.

## Validation

- Focused non-static-aura participant-list consistency tests: `2/2`
- Focused recovery tests: `490/490`
- Adjacent recovery/opening/store-smoke tests: `1071/1071`
- Backend full: `6436/6436`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
