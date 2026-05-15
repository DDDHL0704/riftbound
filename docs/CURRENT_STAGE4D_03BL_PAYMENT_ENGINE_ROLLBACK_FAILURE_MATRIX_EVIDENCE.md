# Stage 4D-03BL-B PaymentEngine Rollback Failure Matrix Evidence

证据日期：2026-05-16
结论：**B-SIDE TEST-ONLY ALL-WINDOW ROLLBACK FAILURE MATRIX VERIFIER COMPLETE / A ACCEPTED / PROJECT NOT READY**

## 1. Changed Files

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_EVIDENCE.md`

`riftbound-dotnet.sln` remains untracked and was not touched.

## 2. Test Evidence

Focused PaymentEngine coverage guard:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Result: **75/75 passed**.

Adjacent PaymentEngine / resource skill / prompt / hub regression:

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: **633/633 passed**.

Backend full:

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4512/4512 passed**.

Diff hygiene:

```sh
git diff --check
```

Result: **passed**.

## 3. Focused Assertions

New verifier methods:

- `PaymentEngineRollbackFailureAllWindowMatrixCoversEveryRequiredSurfaceAndFamily`
- `PaymentEngineRollbackFailureAllWindowMatrixRequiresBoundPromptCommandNoMutationAuditAndDocFields`
- `PaymentEngineRollbackFailureAllWindowMatrixLinksBackTo03BEFamiliesAndSurfaceDocs`
- `PaymentEngineRollbackFailureAllWindowMatrixKeepsFailureDimensionsExecutable`
- `PaymentEngineRollbackFailureAllWindowMatrixDoesNotClaimP0005Closure`

These assertions prove the 4D-03BE rollback failure families now expand into a 42-row executable all-window matrix over `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT` and `BATTLEFIELD_HELD_SCORE_PAYMENT`.

## 4. Acceptance Notes

Verified facts:

- Every required payment surface has all 7 rollback failure families.
- Every rollback family appears across all 6 required payment surfaces.
- Every matrix row binds action window, failure dimension, payment source kind, prompt quote, command rejection, no-mutation assertion, audit expectation and doc anchors.
- Every row links back to `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING`.
- Every row keeps the 4D-03BE family audit and a surface-specific audit doc anchor.
- `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` stay outside this 4D-03BL-B payment rollback matrix.

No closure claims:

- No runtime behavior changed.
- No frontend behavior changed.
- No card matrix JSON changed.
- No `fullOfficial=true` change was made.
- P0-005 remains open.
- The project remains **NOT READY**.
