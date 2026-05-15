# Stage 4D-03BR PaymentEngine Target / Tax Activated Ability Matrix Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / FUTURE B NOT DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current state before a future 4D-03BR-B target / tax activated ability matrix verifier.

The current accepted boundary is 4D-03BQ-B. 4D-03BR does not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Baseline Facts

Current repository state at the start of this baseline:

- Branch: `main`
- Latest commit before this docs-only handoff: `e610fa36 test: verify payment resource skill matrix`
- Expected untracked file: `riftbound-dotnet.sln`

Current PaymentEngine target / tax activated ability baseline:

- `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` remains `covered-representative`.
- `TARGET_TAXES` remains `remaining-official-gap`.
- `TargetColoredActivatedAbilityCoverageManifest` has 8 current catalog ability entries.
- The 8 entries cover target-bearing, typed-color, experience and Spellshield-tax activated ability representatives.
- `SpellshieldTaxCoverageManifest` remains the target-tax prompt / command / audit anchor.
- Future 4D-03BR-B expected target / tax matrix count: 48 rows if it uses the proposed 8 ability entries x 6 target/payment dimensions contract.

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

- Focused PaymentEngine coverage guard: passed 102/102
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 660/660
- Backend full: passed 4539/4539
- `git diff --check`: passed

## 4. Remaining Risk

The baseline is not a full official target/tax matrix and does not prove P0-005 closure. It only records that the next verifier should expand existing target-bearing / typed / experience / Spellshield-tax activated ability representatives into a matrix with prompt / command / audit / rollback / card-matrix trace parity.

Project status remains **NOT READY**.
