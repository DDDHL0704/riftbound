# Stage 4D 03NQ-03NT Payment-Cost Equipment Evidence Bundle Audit

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

The selected rows were chosen because existing evidence already covers representative payment-cost equipment paths:

- Runtime evidence: the matrix already records direct-card behavior for the selected effect kinds.
- Test evidence: existing conformance fixtures and runner tests cover Spinning Axe play / target rejection / assemble attach and Tempered attach interactions, Hearthfire Cloak play / target rejection / assemble attach, Rabadon's Deathcap play / target rejection / assemble attach, and Shurelya's Requiem play / target rejection / ready-all / assemble attach.
- Rules evidence: `docs/rules-evidence-index.md` records audited rules / fixture evidence for the selected play and assemble-equipment paths.
- FAQ evidence: selected rows have no matrix FAQ refs, so no FAQ adjudication is being lowered in this batch.

## Validation Results

Passed checks before commit:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed 665/665.
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~SpinningAxe|FullyQualifiedName~Hearthfire|FullyQualifiedName~Rabadons|FullyQualifiedName~Shurelyas|FullyQualifiedName~TemperedEquipment|FullyQualifiedName~ArmedAssaulter|FullyQualifiedName~JaxTempered"`: passed 70/70.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed 5241/5241.

Frontend build / Chrome smoke are intentionally out of scope because this batch changes only docs/matrix plus the allowed audit-baseline test guard and does not modify frontend, browser scripts or runtime behavior.

## Open Items

This is not final readiness. Remaining open work includes payment-cost residual rows, automated test evidence disposition, Spinning Axe agile / ephemeral lifecycle breadth, Shurelya's Requiem ready-all friendly-unit breadth, complete equipment attach/follow lifecycle breadth, complete LayerEngine / continuous-effect breadth, cleanup / replacement-duration breadth, control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness gates.
