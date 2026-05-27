# Stage 4D-17OC Recovery Spectator Timing Battlefield Task List Payload Shape Audit

Date: 2026-05-28

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in spectator replay-frame timing battlefield-task validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSpectatorReplayFrame`: object-shaped `Timing["battlefieldTasks"][]` entries now emit explicit nested string-list payload-shape diagnostics for malformed non-list `participantControllerIds`, `participantObjectIds` and `stackItemIds` fields before downstream value validation and authoritative parity extraction consume those fields.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `412/412`
- Adjacent recovery/opening/store-smoke tests: `993/993`
- Backend full: `6358/6358`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
