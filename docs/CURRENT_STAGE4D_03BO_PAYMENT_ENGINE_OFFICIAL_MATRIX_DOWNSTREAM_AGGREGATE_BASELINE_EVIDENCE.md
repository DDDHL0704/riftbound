# Stage 4D-03BO PaymentEngine Official Matrix Downstream Aggregate Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / FUTURE B NOT DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current state before a future 4D-03BO-B aggregate verifier.

The current accepted boundary is 4D-03BN. 4D-03BO does not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Baseline Facts

Current repository state at the start of this baseline:

- Branch: `main`
- Latest commit before this docs-only handoff: `90db8563 test: verify payment card matrix alignment`
- Expected untracked file: `riftbound-dotnet.sln`

Current PaymentEngine matrix baseline:

- 9 representative seed rows
- 3 missing official rows
- 1 MOVE_UNIT policy-deferred row
- 7 rollback failure representative families and 42 rollback all-window matrix rows
- 7 cross-window generation / consumption representative families and 42 cross-window all-window matrix rows
- 8 card matrix alignment representative families and 48 card matrix all-window matrix rows

## 3. Baseline Validation

Required A-side baseline validation for this docs-only handoff:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
dotnet test Riftbound.slnx --no-restore
git diff --check
```

Current result:

- Focused PaymentEngine coverage guard: passed 85/85
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 643/643
- Backend full: passed 4522/4522
- `git diff --check`: passed

## 4. Remaining Risk

The baseline is not a full official matrix and does not prove P0-005 closure. It only records that the next verifier should aggregate existing representative downstream matrices back to the official row schema.

Project status remains **NOT READY**.
