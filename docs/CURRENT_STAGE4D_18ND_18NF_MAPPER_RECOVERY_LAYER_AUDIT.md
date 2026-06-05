# Stage 4D-18ND/18NE/18NF Mapper Recovery Layer Audit

Date: 2026-06-06 05:04 CST

Status: accepted into A_MAIN pending checkpoint commit. Project remains **NOT READY**.

## Scope

This bundled server test slice integrated three parallel worker-produced changes:

- 18ND `97c00476`: `ConformanceFixtureShapeTests` adds `GameCommandMapperStrictP0TextArraysTrimStringsAndRejectMalformedItems`, proving strict P0/hand-choice text arrays trim valid strings but return null for blank, null, non-string or unreadable items so runtime validation keeps a stable shape.
- 18NE `a56a4039`: `MatchRecoveryTests` adds `RecoveryValidatorRejectsSnapshotTimingTriggerQueueJhinMovementResourceOriginBattlefieldLocationContextDrift`, proving Jhin movement-resource trigger-queue diagnostics reject source object location drift when a battlefield-origin movement resolves to base from a still-battlefield object location.
- 18NF `5e3f9d02`: `LayerEngineTimestampDependencyTests` adds `LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataRecomputesWhenSourceLeavesBattlefieldAcrossPlayerViews`, proving battlefield static-aura source-order/dependency metadata recomputes after one source leaves and P1/P2 snapshots no longer expose the removed source id.

Runtime changed: no. Matrix JSON changed: no. Frontend, browser/Chrome, official catalog, formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

## Validation

- Focused new tests: `3/3`.
- Touched class filter: `1433/1433`.
- Broader adjacent server filter: `5382/5382`.
- Backend full via tracked `Riftbound.slnx` under current no-DB environment: `7308/7308`.
- `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed before docs sync.

## Remaining Risk

This narrows mapper, recovery and LayerEngine coverage only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
