# Stage 4D-17QA Recovery Timing Continuous Effect Non Static Aura Static Metadata Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects known non-`STATIC_AURA` layers carrying readable non-empty `condition` or `lifecycle` static-aura metadata. Current continuous-effect builders reserve `condition` and `lifecycle` for static-aura effects; temporary `POWER_MODIFIER` and `RULE_TEXT` effects should not carry those static-aura-only metadata values. Malformed and blank optional metadata values keep their existing optional-string diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload non-static-aura metadata diagnostics still run before authoritative parity is skipped.

## Validation

- Focused non-static-aura static-metadata consistency tests: `2/2`
- Focused recovery tests: `494/494`
- Adjacent recovery/opening/store-smoke tests: `1075/1075`
- Backend full: `6440/6440`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
