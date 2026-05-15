# Stage 4D-03BK PaymentEngine Policy-Deferred MOVE_UNIT Boundary Evidence

证据日期：2026-05-16
结论：**ACCEPTED AS TEST-ONLY POLICY-DEFERRED MOVE_UNIT BOUNDARY VERIFIER / PROJECT NOT READY**

## 1. Changed Files

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md`
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

Result: **70/70 passed**.

Adjacent PaymentEngine / resource skill / prompt / hub regression:

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: **628/628 passed**.

Backend full:

```sh
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4507/4507 passed**.

## 3. Focused Assertions

New verifier methods:

- `PaymentEngineOfficialMatrixPolicyDeferredRowsStaySingleMoveUnitBoundary`
- `PaymentEngineOfficialMatrixPolicyDeferredMoveUnitStaysOutOfPaymentManifests`
- `PaymentEngineOfficialMatrixPolicyDeferredMoveUnitDoesNotClaimPaymentCoverageOrClosure`

These assertions prove the current `policy-deferred-row` remains a single MOVE_UNIT movement-permission boundary and stays outside PaymentEngine payment manifests. They do not prove full official PaymentEngine matrix closure.

## 4. Acceptance Notes

Accepted facts:

- Policy-deferred rows are exactly 1 row.
- The only policy-deferred row is `ROW_MOVE_UNIT_POLICY_DEFERRED`.
- MOVE_UNIT remains linked to `ACTION_WINDOWS` as policy movement permission metadata.
- MOVE_UNIT is excluded from representative seed, missing official, rollback failure, cross-window generation / consumption, and card matrix alignment manifests.
- MOVE_UNIT remains `policy-non-resource` in `CoverageManifest`.
- MOVE_UNIT remains `policy-deferred` in `ResidualBlockerManifest`.
- All covered text keeps NOT READY / P0-005-open closure language.

No closure claims:

- No runtime behavior changed.
- No frontend behavior changed.
- No card matrix JSON changed.
- No `fullOfficial=true` change was made.
- P0-005 remains open.
- The project remains **NOT READY**.
