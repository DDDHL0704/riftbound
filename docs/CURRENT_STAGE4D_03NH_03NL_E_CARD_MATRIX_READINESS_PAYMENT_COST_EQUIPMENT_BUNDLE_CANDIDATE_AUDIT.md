# Stage 4D 03NH-03NL Payment-Cost Equipment Bundle Audit

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

The selected rows were chosen because existing evidence already covers the representative payment-cost and equipment paths:

- Runtime registry/profile evidence: `CardBehaviorRegistry`, `CoreRuleEngine`, and `MatchSession` already contain the selected equipment play and attach profiles.
- Test evidence: existing conformance fixtures and runner tests cover play, explicit-target rejection, and the relevant assemble command representatives.
- Rules evidence: `docs/rules-evidence-index.md` records `RULE_AUDITED` rows for the selected play and assemble paths.
- FAQ evidence: selected rows have no matrix FAQ refs, so no FAQ adjudication is being lowered in this batch.

## Validation Results

Required checks passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- focused `PaymentEngineCoverageAuditTests`: 662/662 passed
- selected-row focused evidence filter `Cull|LastRites|VanguardsEye|BfSword|SacredShears`: 20/20 passed
- backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: 5238/5238 passed

## Open Items

This is not final readiness. Remaining open work includes payment-cost residual rows, automated test evidence disposition, full equipment attach/follow lifecycle breadth, Last Rites hidden-info breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness gates.
