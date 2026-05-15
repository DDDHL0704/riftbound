# Stage 4D-03BQ PaymentEngine Resource Skill All-Window Matrix Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / FUTURE B NOT DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current state before a future 4D-03BQ-B resource skill all-window matrix verifier.

The current accepted boundary is 4D-03BP-B. 4D-03BQ does not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Baseline Facts

Current repository state at the start of this baseline:

- Branch: `main`
- Latest commit before this docs-only handoff: `332b621c test: verify payment keyword matrix`
- Expected untracked file: `riftbound-dotnet.sln`

Current PaymentEngine resource skill baseline:

- `RESOURCE_SKILL_A_C_FAMILY` remains `catalog-bound-representative`
- `RESOURCE_SKILLS` remains `remaining-official-gap`
- 6 resource skill family manifest entries exist after 4D-03AZ
- 19 current catalog `IsResourceSkill=true` ability ids are covered by those families
- 6 current PaymentEngine payment surfaces are used by the recent all-window matrices
- Future 4D-03BQ-B expected all-window matrix count: 36 family-window rows

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

- Focused PaymentEngine coverage guard: passed 97/97
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 655/655
- Backend full: passed 4534/4534
- `git diff --check`: passed

## 4. Remaining Risk

The baseline is not a full official resource skill matrix and does not prove P0-005 closure. It only records that the next verifier should expand existing resource skill representatives into an all-window matrix with prompt / command / audit / generated-resource lifetime / rollback parity.

Project status remains **NOT READY**.
