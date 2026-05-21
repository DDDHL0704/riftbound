# Stage 4D 03NM-03NP Payment-Cost Mixed Evidence Bundle Audit

Status: validated.

## Scope

Allowed write scope used:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- current checkpoint / audit / coverage docs
- this candidate/audit pair
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` only for residual expected-count / current-slice manifest guard synchronization

Locked scope preserved:

- runtime, frontend, API / protocol core fields, official catalog, general tests outside `PaymentEngineCoverageAuditTests.cs`, browser / Chrome / formal E2E scripts, `fullOfficial`, final readiness state and `riftbound-dotnet.sln`

## Evidence Review

The selected rows were chosen because existing evidence already covers the representative payment-cost paths:

- Runtime evidence: `CardBehaviorRegistry` already contains the selected play profiles; Blade of the Ruined King also has existing equipment attach / destroy-friendly-unit server support.
- Test evidence: existing conformance fixtures and runner tests cover Marching Orders mutual-power damage and enemy-base target rejection, Blade of the Ruined King play / target rejection / assemble destroy-friendly-unit, Power Bind Echo two-friendly power modification and append order, and Danger Temperature mechanical-only power modification.
- Rules evidence: `docs/rules-evidence-index.md` records audited rules / fixture evidence for the selected effects.
- FAQ evidence: selected rows have no matrix FAQ refs, so no FAQ adjudication is being lowered in this batch.

## Validation Results

Required checks passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- focused `PaymentEngineCoverageAuditTests`: 663/663 passed
- selected-row focused evidence filter `MarchingOrders|BladeOfRuinedKing|PowerBind|DangerTemperature`: 9/9 passed
- backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: 5239/5239 passed

## Open Items

This is not final readiness. Remaining open work includes payment-cost residual rows, automated test evidence disposition, Marching Orders battle / spell-duel lifecycle breadth, Blade of the Ruined King complete equipment lifecycle breadth, Power Bind Echo / payment-branch breadth, Danger Temperature mechanical-tag official breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness gates.
