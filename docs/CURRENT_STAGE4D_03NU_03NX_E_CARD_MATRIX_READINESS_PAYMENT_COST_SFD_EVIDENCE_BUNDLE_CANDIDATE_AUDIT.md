# Stage 4D 03NU-03NX Payment-Cost SFD Evidence Bundle Audit

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
- Test evidence: existing conformance fixtures and runner tests cover Vanguard Armory play / target rejection, Karina Veraze no-optional-Haste / optional-ready branch, Arcane Shift friendly-banish / enemy-damage / target rejection, and Laurent Duelist counter-spell power-by-cost / invalid target rejection.
- Rules evidence: `docs/rules-evidence-index.md` records audited rules / fixture evidence for the selected play paths.
- FAQ evidence: selected rows have no matrix FAQ refs, so no FAQ adjudication is being lowered in this batch.

## Validation Results

Passed checks before commit:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed 667/667.
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~VanguardArmory|FullyQualifiedName~KarinaVeraze|FullyQualifiedName~ArcaneShift|FullyQualifiedName~LaurentDuelist"`: passed 10/10.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed 5243/5243.

Frontend build / Chrome smoke are intentionally out of scope because this batch changes only docs/matrix plus the allowed audit-baseline test guard and does not modify frontend, browser scripts or runtime behavior.

## Open Items

This is not final readiness. Remaining open work includes payment-cost residual rows, automated test evidence disposition, Vanguard Armory equipment activated-skill breadth, Karina Veraze Haste optional-cost breadth, Arcane Shift banish / replay ownership-control and battle / spell-duel breadth, Laurent Duelist counter-spell target / cleanup breadth, complete FEPR target / stack timing, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness gates.
