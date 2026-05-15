# Stage 4D-03BM PaymentEngine Cross-Window Generation Consumption Matrix Evidence

Evidence date: 2026-05-16
Conclusion: **B-SIDE TEST-ONLY ALL-WINDOW CROSS-WINDOW MATRIX VERIFIER COMPLETE / A ACCEPTED / PROJECT NOT READY**

## 1. Changed Files

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_EVIDENCE.md`

`riftbound-dotnet.sln` remains untracked and was not touched.

## 2. Test Evidence

Focused PaymentEngine coverage guard:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Result: **80/80 passed**.

Diff hygiene:

```sh
git diff --check
```

Result: **passed**.

A-side acceptance validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Result: **80/80 passed**.

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: **638/638 passed**.

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4517/4517 passed**.

## 3. Focused Assertions

New verifier methods:

- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixCoversEveryRequiredSurfaceAndFamily`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixRequiresBoundLifecycleAndAuditFields`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixLinksBackTo03BFFamiliesAnd03BMDocs`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixKeepsLifecycleDimensionsExecutable`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixDoesNotClaimP0005Closure`

These assertions prove the 4D-03BF cross-window families now expand into a 42-row executable representative matrix over `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT` and `BATTLEFIELD_HELD_SCORE_PAYMENT`.

## 4. Acceptance Notes

Verified contract:

- Every required payment surface has all 7 cross-window generation / consumption families.
- Every cross-window family appears across all 6 required payment surfaces.
- Every matrix row binds action window, generation scope, consumption scope, resource / lifetime dimension, prompt quote, command commit or rejection anchor, audit expectation, lifetime / no-mutation / restriction assertion and doc anchors.
- Every row links back to `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`.
- Every row keeps the 4D-03BF family audit and the 4D-03BM matrix audit doc anchor.
- `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` stay outside this 4D-03BM payment matrix.

No closure claims:

- No runtime behavior changed.
- No frontend behavior changed.
- No card matrix JSON changed.
- No `fullOfficial=true` change was made.
- P0-005 remains open.
- The project remains **NOT READY**.
