# Stage 4D-03BP PaymentEngine Keyword Branch All-Window Matrix Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / FUTURE B NOT DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current state before a future 4D-03BP-B keyword branch all-window matrix verifier.

The current accepted boundary is 4D-03BO-B. 4D-03BP does not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Baseline Facts

Current repository state at the start of this baseline:

- Branch: `main`
- Latest commit before this docs-only handoff: `bc13b0f0 test: verify payment matrix aggregate`
- Expected untracked file: `riftbound-dotnet.sln`

Current PaymentEngine keyword branch baseline:

- `KEYWORD_PAYMENT_BRANCHES` remains `remaining-official-gap`
- 8 keyword branch manifest entries exist after 4D-03AY
- 6 current PaymentEngine payment surfaces are used by the recent all-window matrices
- Future 4D-03BP-B expected all-window matrix count: 48 rows

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

- Focused PaymentEngine coverage guard: passed 92/92
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 650/650
- Backend full: passed 4529/4529
- `git diff --check`: passed

## 4. Remaining Risk

The baseline is not a full official keyword payment matrix and does not prove P0-005 closure. It only records that the next verifier should expand existing keyword branch representatives into an all-window matrix with prompt / command / audit / rollback parity.

Project status remains **NOT READY**.
