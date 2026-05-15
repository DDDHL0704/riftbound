# Stage 4D-03BM PaymentEngine Cross-Window Generation Consumption Matrix Audit

Audit date: 2026-05-16
Conclusion: **B-SIDE VERIFIER COMPLETE / A ACCEPTED / PROJECT NOT READY**

## 1. Scope

This batch follows 4D-03BF and expands `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING` from the 7 representative family manifest into an executable all-window representative matrix.

This batch only changes conformance tests and 4D-03BM docs. It does not change runtime behavior, frontend behavior, card matrix JSON, `fullOfficial`, READY status, P0-005 closure, or `riftbound-dotnet.sln`.

## 2. Matrix Contract

`PaymentEngineCoverageAuditTests` now builds a 42-row cross-window generation / consumption matrix:

- 6 current PaymentEngine payment surfaces: `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT`, `BATTLEFIELD_HELD_SCORE_PAYMENT`
- 7 4D-03BF cross-window representative families: `RESOURCE_SKILL_GENERATION_WINDOWS`, `INLINE_PAYMENT_CONSUMPTION_WINDOWS`, `PENDING_PAYMENT_REUSE_AND_CLOSE`, `TYPED_GENERIC_CONVERSION_AND_MATCHING`, `EXPIRY_CLEANUP_AND_TURN_BOUNDARY`, `PAYMENT_ONLY_RESTRICTIONS_AND_WRONG_WINDOW`, `DUPLICATE_SPEND_AND_AUDIT_CORRELATION`

Every matrix row must bind:

- action window
- generation scope
- consumption scope
- resource / lifetime dimension
- prompt quote
- command commit or rejection anchor
- audit expectation
- lifetime / no-mutation / restriction assertion
- remaining official breadth
- closure status
- doc anchors

Every row links back to `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`, the 03BF family manifest, and this 03BM audit doc.

## 3. Guards

New focused verifier methods:

- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixCoversEveryRequiredSurfaceAndFamily`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixRequiresBoundLifecycleAndAuditFields`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixLinksBackTo03BFFamiliesAnd03BMDocs`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixKeepsLifecycleDimensionsExecutable`
- `PaymentEngineCrossWindowGenerationConsumptionAllWindowMatrixDoesNotClaimP0005Closure`

These guards prove the representative all-window matrix is executable and auditable without promoting it to full official coverage.

Boundary guards:

- `MOVE_UNIT` is not included.
- `HIDE_CARD` is not included.
- `LEGEND_ACT` is not included.
- `fullOfficial=true` is not introduced.
- P0-005 remains open.
- The project remains **NOT READY**.

## 4. Validation

B-side focused validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Result: **80/80 passed**.

A-side acceptance validation:

- Focused PaymentEngine coverage guard: **80/80 passed**
- Adjacent PaymentEngine / resource skill / prompt / hub regression: **638/638 passed**
- Backend full `dotnet test Riftbound.slnx --no-restore`: **4517/4517 passed**
- `git diff --check`: **passed**

A accepted the B-side diff because it stayed inside the 4D-03BM test/docs write lock and did not touch runtime, frontend, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.

## 5. Remaining Risk

4D-03BM proves a representative all-window matrix contract for generated-resource creation, consumption, expiry, cleanup, restrictions, duplicate-spend rejection and audit correlation.

It still does not prove full official PaymentEngine closure. The remaining gaps include every official card row, every generated-resource source, all typed/generic/resource conversion mixes, every cleanup ordering, all invalid reuse branches, card matrix JSON alignment, frontend final validation, P0/P1 closure and completion audit READY.

Project status remains **NOT READY**; P0-005 remains open.
