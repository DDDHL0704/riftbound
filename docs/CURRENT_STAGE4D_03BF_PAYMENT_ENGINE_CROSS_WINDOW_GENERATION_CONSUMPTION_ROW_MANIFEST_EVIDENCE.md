# Stage 4D-03BF PaymentEngine Cross-Window Generation Consumption Row Manifest Evidence

证据日期：2026-05-16
结论：**ACCEPTED AS TEST-ONLY CROSS-WINDOW ROW VERIFIER / PROJECT NOT READY**

## 1. Changed Files

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`

`riftbound-dotnet.sln` remains untracked and was not touched.

## 2. Test Evidence

Focused PaymentEngine coverage guard:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Result: **56/56 passed**.

Adjacent PaymentEngine / resource skill / prompt / hub regression:

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: **614/614 passed**.

Backend full:

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4493/4493 passed**.

## 3. Focused Assertions

New verifier methods:

- `PaymentEngineCrossWindowGenerationConsumptionManifestListsRequiredFamiliesExactlyOnce`
- `PaymentEngineCrossWindowGenerationConsumptionManifestRequiresPromptCommandAuditLifetimeAndDocAnchors`
- `PaymentEngineCrossWindowGenerationConsumptionRowsStayLinkedToOfficialMatrixMissingRow`
- `PaymentEngineCrossWindowGenerationConsumptionRowsKeepLifecycleDimensionsExplicit`
- `PaymentEngineCrossWindowGenerationConsumptionRowsDoNotClaimP0005Closure`

These assertions prove the cross-window generation / consumption missing row is decomposed into auditable representative families. They do not prove full official PaymentEngine behavior.

## 4. Acceptance Notes

Accepted facts:

- 7 cross-window generation / consumption families are fixed exactly once.
- Every row remains linked to `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`.
- Each row preserves prompt / command / audit / no-mutation / docs anchors.
- The manifest keeps generation, creation, consumption, payment-only restriction, expiry, cleanup, typed / generic matching, pending payment close, duplicate spend and audit-correlation dimensions visible.

No closure claims:

- No runtime behavior changed.
- No frontend behavior changed.
- No card matrix JSON changed.
- No `fullOfficial=true` change was made.
- P0-005 remains open.
- The project remains **NOT READY**.
