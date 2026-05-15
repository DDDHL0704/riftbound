# Stage 4D-03BN PaymentEngine Card Matrix Alignment Matrix Audit

Audit date: 2026-05-16
Conclusion: **B-SIDE VERIFIER COMPLETE / A ACCEPTED / PROJECT NOT READY**

## 1. Scope

This batch follows 4D-03BG and expands `ROW_CARD_MATRIX_ALIGNMENT_MISSING` from an 8-family representative row manifest into an executable all-window representative matrix.

This batch only changes the conformance verifier and 4D-03BN docs. It does not change runtime behavior, frontend behavior, card matrix JSON, `fullOfficial`, READY status, P0-005 closure, or `riftbound-dotnet.sln`.

## 2. Matrix Contract

`PaymentEngineCoverageAuditTests` now builds a 48-row card matrix alignment matrix:

- 6 current PaymentEngine payment surfaces: `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT`, `BATTLEFIELD_HELD_SCORE_PAYMENT`
- 8 4D-03BG card matrix alignment families: `MATRIX_ID_AND_STATUS_FIELDS`, `PAYMENT_ROW_TO_CARD_MATRIX_MAPPING`, `REPRESENTATIVE_TEST_EVIDENCE_LINKS`, `FULL_OFFICIAL_GATE_AND_COMPLETION_BLOCK`, `FAQ_RULE_SOURCE_TRACE`, `FRONTEND_CONTRACT_AND_SNAPSHOT_TRACE`, `MATRIX_JSON_SYNC_AND_DRIFT_GUARD`, `DEFERRED_BLOCKER_AND_STATUS_COUNTS`

Every matrix row must bind:

- action window
- matrix scope
- representative surface
- prompt evidence anchor
- command evidence anchor
- audit evidence anchor
- matrix sync/status anchor
- frontend/snapshot or rule-source trace as appropriate
- remaining official breadth
- closure status
- doc anchors

Every row links back to `ROW_CARD_MATRIX_ALIGNMENT_MISSING`, the 4D-03BG family manifest, this 4D-03BN audit doc, and the 4D-03BN evidence doc.

## 3. Guards

New focused verifier methods:

- `PaymentEngineCardMatrixAlignmentAllWindowMatrixCoversEveryRequiredSurfaceAndFamily`
- `PaymentEngineCardMatrixAlignmentAllWindowMatrixRequiresBoundEvidenceAndTraceFields`
- `PaymentEngineCardMatrixAlignmentAllWindowMatrixLinksBackTo03BGFamiliesAnd03BNDocs`
- `PaymentEngineCardMatrixAlignmentAllWindowMatrixKeepsMatrixDimensionsExecutable`
- `PaymentEngineCardMatrixAlignmentAllWindowMatrixDoesNotClaimFullOfficialOrP0005Closure`

These guards prove that the representative all-window matrix is executable and auditable without promoting card matrix rows to full official coverage.

Boundary guards:

- `MOVE_UNIT` is not included.
- `HIDE_CARD` is not included.
- `LEGEND_ACT` is not included.
- Card matrix JSON is not modified.
- `fullOfficial=true` is not introduced.
- P0-005 remains open.
- The project remains **NOT READY**.

## 4. Validation Evidence

B-side focused validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
git diff --check
```

Result:

- Focused PaymentEngine coverage guard: **85/85 passed**
- `git diff --check`: **passed**

A-side acceptance validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result:

- Focused PaymentEngine coverage guard: **85/85 passed**
- Adjacent PaymentEngine / resource skill / prompt / hub regression: **643/643 passed**
- Backend full: **4522/4522 passed**
- `git diff --check`: **passed**

A accepts this slice as a test/docs-only verifier because it stays inside the assigned scope and keeps runtime, frontend, card matrix JSON, `fullOfficial`, READY, P0-005 closure and `riftbound-dotnet.sln` unchanged.

## 5. Remaining Risk

4D-03BN proves a representative all-window card matrix alignment contract for current PaymentEngine payment surfaces and 4D-03BG families.

It still does not prove full official PaymentEngine or card matrix closure. The remaining gaps include every official card row, every collector/oracle variant, every effect row, all branches, all FAQ/rule-source blockers, all frontend final validation, card matrix JSON synchronization, P0/P1 closure and completion audit READY.

Project status remains **NOT READY**; P0-005 remains open.
