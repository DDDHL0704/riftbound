# Stage 4D-03BN PaymentEngine Card Matrix Alignment Matrix Evidence

Audit date: 2026-05-16
Conclusion: **B-SIDE REPRESENTATIVE MATRIX EVIDENCE COMPLETE / A ACCEPTED / PROJECT NOT READY**

## 1. Evidence Scope

This evidence file records the 4D-03BN executable matrix contract for `ROW_CARD_MATRIX_ALIGNMENT_MISSING`.

The matrix is representative. It links the 8 4D-03BG card matrix alignment families across the 6 current PaymentEngine payment surfaces, for 48 rows total. It does not edit card matrix JSON, does not set `fullOfficial=true`, does not close P0-005, and does not declare READY.

## 2. Row Shape

Each generated matrix row binds the following evidence fields:

- `MatrixRowId`
- `Family`
- `OfficialMatrixRowId = ROW_CARD_MATRIX_ALIGNMENT_MISSING`
- `ActionWindow`
- `MatrixScope`
- `RepresentativeSurface`
- `PromptEvidenceAnchor`
- `CommandEvidenceAnchor`
- `AuditEvidenceAnchor`
- `MatrixSyncStatusAnchor`
- `FrontendSnapshotOrRuleSourceTrace`
- `RemainingOfficialBreadth`
- `ClosureStatus`
- `DocAnchors`

The verifier requires every row to include `docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md`, `docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md`, and this evidence doc.

## 3. Covered Representative Surfaces

The matrix covers these PaymentEngine payment surfaces only:

- `PLAY_CARD`
- `PAY_COST`
- `ACTIVATE_ABILITY`
- `ASSEMBLE_EQUIPMENT`
- `TRIGGER_PAYMENT`
- `BATTLEFIELD_HELD_SCORE_PAYMENT`

The matrix explicitly excludes `MOVE_UNIT`, `HIDE_CARD`, and `LEGEND_ACT`.

## 4. Covered 4D-03BG Families

The matrix reuses the 4D-03BG family manifest exactly once per surface:

- `MATRIX_ID_AND_STATUS_FIELDS`
- `PAYMENT_ROW_TO_CARD_MATRIX_MAPPING`
- `REPRESENTATIVE_TEST_EVIDENCE_LINKS`
- `FULL_OFFICIAL_GATE_AND_COMPLETION_BLOCK`
- `FAQ_RULE_SOURCE_TRACE`
- `FRONTEND_CONTRACT_AND_SNAPSHOT_TRACE`
- `MATRIX_JSON_SYNC_AND_DRIFT_GUARD`
- `DEFERRED_BLOCKER_AND_STATUS_COUNTS`

This keeps 4D-03BG as the family-level source of truth and 4D-03BN as the executable all-window representative matrix.

## 5. Validation Evidence

Required local validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
git diff --check
```

Current result:

- Focused PaymentEngine coverage guard: **85/85 passed**
- `git diff --check`: **passed**

A-side acceptance result:

- Focused PaymentEngine coverage guard: **85/85 passed**
- Adjacent PaymentEngine / resource skill / prompt / hub regression: **643/643 passed**
- Backend full: **4522/4522 passed**
- `git diff --check`: **passed**

This result accepts the 48-row representative matrix as executable evidence only. It does not promote card matrix JSON, `fullOfficial`, P0-005, P1 or READY.

## 6. Remaining Risk

The 48-row verifier is not a full official card matrix. Remaining official breadth includes all official card/effect rows, cardId/collectorId/oracleId/effectId variants, FAQ/rule-source conflicts, blocker-count reconciliation, JSON status sync, frontend final validation, P0/P1 closure and completion audit READY.
