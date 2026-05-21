4D-03LZ-E payment-cost Heart of Ice equipment cleanup blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03LY-E 后的第一百七十二枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03LzPaymentCostHeartOfIceEquipmentCleanupBlockerClosureCandidate`。`Post03LzCardMatrixReadinessPaymentCostHeartOfIceEquipmentCleanupBlockerClosureCandidateManifest` records selected functionalUnit=FU-35e1c62c46；selected card=SFD·052/221 玄冰之心；selected effect=HEART_OF_ICE_PLAY_EQUIPMENT；NEEDS_ENGINE_SUPPORT 189 -> 188；primary residual 135 -> 134；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 376 -> 375；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 121 -> 121；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Heart of Ice automated evidence disposition、tap-to-grant-power equipment skill breadth、cleanup/replacement duration breadth、complete equipment activated-skill matrix、complete PaymentEngine / PAY_COST matrix 与 formal 18-step E2E 仍 open；Chrome smoke not run for 03LZ because there were no frontend or browser-script changes；matrix JSON valid (jq empty); 03LZ matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 621/621; Heart of Ice focused regression 5/5; adjacent prompt/payment/equipment/cleanup regression 1105/1105; backend full test 5192/5192; git diff --check passed.

# 4D-03LZ-E card matrix readiness slice

## Selected row

- functionalUnitId: `FU-35e1c62c46`
- card: `SFD·052/221` / 玄冰之心
- effect: `HEART_OF_ICE_PLAY_EQUIPMENT`
- selected matrix row: `payment-cost`
- secondary row query: `payment-cost-equipment-cleanup`

## Evidence accepted for this slice

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the fixed official card/effect row for 玄冰之心 with zero-target equipment play behavior.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-heart-of-ice-equipment.fixture.json` covers paying 3, creating the zero-target stack item, and resolving the source as a base equipment object.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-heart-of-ice-target-rejected.fixture.json` covers rejecting an explicit target before payment, zone movement, equipment entry, or stack creation.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, and `docs/CURRENT_P4_STATUS.md` record rule-audited evidence for the Heart of Ice equipment entry and target rejection paths.

## Matrix transition

- `NEEDS_ENGINE_SUPPORT`: `189 -> 188`
- primary residual: `135 -> 134`
- `payment-or-targeting-stack-timing`: `376 -> 375`
- `payment-and-targeting-stack-timing`: `121 -> 121`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains `328`
- `NEEDS_FAQ_REVIEW`: remains `92`
- `fullOfficialTrue`: remains `0`
- final readiness: remains `false`

## Non-closure

This slice does not close Heart of Ice automated evidence disposition, tap-to-grant-power equipment skill breadth, cleanup/replacement duration breadth, complete equipment activated-skill matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, P0/P1, or READY.
