# Stage 4D 03OD-03OF Payment-Cost SFD Direct Evidence Bundle Audit

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
- Test evidence: existing conformance fixtures and runner tests cover Protect the Emperor sand-soldier token creation, Zaunite Thug no-optional-equipment play plus explicit-target rejection, and Quicksand Pit stack resolution destroying a battlefield unit.
- Rules evidence: `docs/rules-evidence-index.md` records audited rules / fixture evidence for `p2-preflight-play-protect-the-emperor-create-sand-soldier`, `p2-preflight-play-zaunite-thug-no-optional-equipment-static`, `p4-play-zaunite-thug-target-rejected`, and `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack`.
- FAQ evidence: selected rows have no matrix FAQ refs, so no FAQ adjudication is being lowered in this batch.

## Validation Results

Passed checks before commit:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `git diff --check`: passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed 671/671.
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ProtectTheEmperor|FullyQualifiedName~ZauniteThug|FullyQualifiedName~QuicksandPit"`: passed 3/3.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed 5247/5247.

Frontend build / Chrome smoke are intentionally out of scope because this batch changes only docs/matrix plus the allowed audit-baseline test guard and does not modify frontend, browser scripts or runtime behavior.

## Open Items

This is not final readiness. Remaining open work includes payment-cost residual rows, automated test evidence disposition, Protect the Emperor yellow optional-ready branch, Zaunite Thug optional friendly equipment destroy branch, Quicksand Pit non-hand cost-reduction path, battle / cleanup / hidden / targeting-stack breadth, complete FEPR target / stack timing, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness gates.
