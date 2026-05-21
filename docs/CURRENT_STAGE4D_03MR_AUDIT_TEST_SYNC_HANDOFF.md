# 4D-03MR Audit-Test Sync Handoff

Date: 2026-05-21

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`

Base commit: `6cea1181`

## Decision

4D-03MR must not proceed as a pure docs-only matrix reduction. The Jayce row has enough existing runtime, fixture and rules evidence to justify a candidate draft, but the current audit-test baseline still hard-codes the 4D-03MQ payment-cost residual at 171. Reducing the matrix only would make the matrix and `PaymentEngineCoverageAuditTests` disagree.

No failed matrix draft is committed. No `tests/**` files are changed in this handoff.

## Candidate Evidence

- selected functionalUnit: `FU-51de703f12`
- selected card / collectorId: `SFD·084/221` / cardId `33166`
- selected card name: `杰斯`
- selected effect / oracle: `SFD_JAYCE_NO_OPTIONAL_EQUIPMENT_PLAY_UNIT`
- current categories: `cleanup-replacement-duration`, `hidden-info-random-zone`, `payment-cost`, `targeting-stack-timing`
- current blockers: `NEEDS_ENGINE_SUPPORT`, `NEEDS_AUTOMATED_TEST_EVIDENCE`
- FAQ status: no FAQ candidate in matrix

Existing evidence:

- Runtime: `src/Riftbound.Engine/CardBehaviorRegistry.cs` has direct-card-behavior metadata for `SFD·084/221` / `SFD_JAYCE_NO_OPTIONAL_EQUIPMENT_PLAY_UNIT`.
- Fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-jayce-no-optional-equipment-static.fixture.json` covers the no-optional-equipment normal play path.
- Fixture: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-sfd-jayce-target-rejected.fixture.json` covers explicit target rejection for the current zero-target play path.
- Rules evidence: `docs/rules-evidence-index.md` records both `p2-preflight-play-sfd-jayce-no-optional-equipment-static` and `p4-play-sfd-jayce-target-rejected` as `RULE_AUDITED` with `CATALOG` `SFD·084/221` and `CORE-260330` refs.

## Draft Impact If A Opens Sync Batch

A coordinated matrix + audit-test baseline sync could remove only the engine-support blocker for the selected Jayce row while keeping automated evidence open.

Expected blocker-count effects from the current 4D-03MQ baseline:

- all functional-unit `NEEDS_ENGINE_SUPPORT`: `569 -> 568`
- `payment-cost` `NEEDS_ENGINE_SUPPORT`: `171 -> 170`
- `targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `294 -> 293`
- `cleanup-replacement-duration` `NEEDS_ENGINE_SUPPORT`: `217 -> 216`
- `hidden-info-random-zone` `NEEDS_ENGINE_SUPPORT`: `178 -> 177`
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `358 -> 357`
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: `107 -> 106`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains open for Jayce
- `fullOfficialTrue`: remains `0`
- final readiness flags remain unchanged

## Blocking Audit Baseline

The current audit tests still encode the 4D-03MQ post-state as the accepted baseline:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` currently asserts `CountPost03EfMatrixRowsWithBlocker(functionalUnits, "payment-cost", "NEEDS_ENGINE_SUPPORT") == 171`.
- The 4D-03MQ closure candidate guard also asserts `rowCountContinuity.needsEngineSupportFunctionalUnitsAfter == 171`.
- The 4D-03MQ manifest chain asserts the previous candidate's `PrimaryNeedsEngineSupportAfter == 171`.

Therefore a matrix-only Jayce draft that changes payment-cost `171 -> 170` cannot pass the audit suite. This is not a Jayce runtime or evidence failure; it is an audit baseline synchronization boundary.

## Minimum Write Locks Required From A

A should open a narrow 03MR matrix + audit-test baseline sync batch with these write locks:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- current matrix/checkpoint docs needed to describe 03MR acceptance and non-closure status
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` only for the minimal residual expected-count and current-slice manifest assertions affected by the Jayce matrix change

Still locked:

- `src/Riftbound.Engine/**`
- `src/Riftbound.Api/**`
- `src/Riftbound.DevUi/**`
- all tests except the narrow audit-baseline assertions above
- official snapshot/catalog data
- frontend/browser/formal E2E artifacts
- `riftbound-dotnet.sln`

## Stop State

This handoff records the blocker and stops. It does not reduce the matrix, does not commit a failed draft, does not continue to another card, and does not change project readiness status.
