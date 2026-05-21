# Stage 4D 03NY-03OC Payment-Cost SFD Evidence Bundle Audit

Status: validated; validation passed for this commit.

## Scope

Allowed write scope used:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- current checkpoint / audit / coverage docs
- this candidate/audit pair
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` only for residual expected-count / current-slice manifest guard synchronization

Locked scope preserved:

- runtime, frontend, API / protocol core fields, official catalog, general tests outside `PaymentEngineCoverageAuditTests.cs`, browser / Chrome / formal E2E scripts, `fullOfficial`, final readiness state and `riftbound-dotnet.sln`

## Evidence Review

The selected rows were chosen because existing evidence already covers representative payment-cost SFD paths:

- Runtime evidence: the matrix already records direct-card behavior for the selected effect kinds.
- Test evidence: existing conformance fixtures and runner tests cover Predictive Offensive draw / recycle and rejection paths, Master Bingwen no-optional-assemble, Ancient Berserker no-optional-Haste plus optional-ready branch, Windsong Wing return-small-battlefield / no-target / invalid target, and SFD Fizz normal graveyard-spell static play.
- Rules evidence: `docs/rules-evidence-index.md` records audited rules / fixture evidence for the selected play paths.
- FAQ evidence: selected rows have no matrix FAQ refs, so no FAQ adjudication is being lowered in this batch.

## Validation Results

Passed checks before commit:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed 669/669.
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PredictiveOffensive|FullyQualifiedName~MasterBingwen|FullyQualifiedName~AncientBerserker|FullyQualifiedName~WindsongWing|FullyQualifiedName~Fizz"`: passed 7/7.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed 5245/5245.

Frontend build / Chrome smoke are intentionally out of scope because this batch changes only docs/matrix plus the allowed audit-baseline test guard and does not modify frontend, browser scripts or runtime behavior.

## Open Items

This is not final readiness. Remaining open work includes payment-cost residual rows, automated test evidence disposition, Predictive Offensive Echo / replay breadth, Master Bingwen Tempered assemble / armament attach breadth, Ancient Berserker exact Haste resource breadth, Windsong Wing standby / reaction placement breadth, SFD Fizz graveyard spell selection / free-play breadth, hidden-info / control-zone / layer / battle-spell-duel breadth, complete FEPR target / stack timing, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness gates.
