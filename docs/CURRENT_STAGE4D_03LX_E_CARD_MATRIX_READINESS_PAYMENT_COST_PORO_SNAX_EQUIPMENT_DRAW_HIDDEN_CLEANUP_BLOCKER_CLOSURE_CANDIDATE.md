4D-03LX-E payment-cost Poro Snax equipment draw hidden/cleanup blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03LW-E 后的第一百七十枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03LxPaymentCostPoroSnaxEquipmentDrawHiddenCleanupBlockerClosureCandidate`。`Post03LxCardMatrixReadinessPaymentCostPoroSnaxEquipmentDrawHiddenCleanupBlockerClosureCandidateManifest` records selected functionalUnit=FU-fed25402b8；selected card=SFD·046/221 魄罗佳肴；selected effect=PORO_SNAX_PLAY_EQUIPMENT_DRAW_1；NEEDS_ENGINE_SUPPORT 191 -> 190；primary residual 136 -> 135；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 378 -> 377；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 121 -> 121；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Poro Snax automated evidence disposition、hidden-info draw visibility breadth、activated destroy-self draw breadth、cleanup/replacement duration breadth、complete PaymentEngine / PAY_COST matrix 与 formal 18-step E2E 仍 open；Chrome smoke not run for 03LX because there were no frontend or browser-script changes；matrix JSON valid (jq empty); 03LX matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 617/617; Poro Snax focused regression 5/5; adjacent prompt/payment/equipment/draw/hidden/cleanup regression 2392/2392; backend full test 5188/5188; git diff --check passed.

# 4D-03LX-E card matrix readiness slice

## Selected row

- functionalUnitId: `FU-fed25402b8`
- card: `SFD·046/221` / 魄罗佳肴
- effect: `PORO_SNAX_PLAY_EQUIPMENT_DRAW_1`
- selected matrix row: `payment-cost`
- secondary row query: `payment-cost-hidden-cleanup`

## Evidence accepted for this slice

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the fixed official card/effect row for 魄罗佳肴 with zero-target equipment play and draw-one behavior.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-poro-snax-equipment-draw.fixture.json` covers paying 1, creating the zero-target stack item, resolving the source as a base equipment object, and drawing one card from the main deck.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-poro-snax-target-rejected.fixture.json` covers rejecting an explicit target before payment, zone movement, equipment entry, draw, or stack creation.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` runs the accepted fixture, direct target-rejection path, and P4 rejection fixture.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, and `docs/CURRENT_P4_STATUS.md` record rule-audited evidence for the Poro Snax equipment draw and target-rejection paths.

## Matrix transition

- `NEEDS_ENGINE_SUPPORT`: `191 -> 190`
- primary residual: `136 -> 135`
- `payment-or-targeting-stack-timing`: `378 -> 377`
- `payment-and-targeting-stack-timing`: `121 -> 121`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains `328`
- `NEEDS_FAQ_REVIEW`: remains `92`
- `fullOfficialTrue`: remains `0`
- final readiness: remains `false`

## Non-closure

This slice does not close Poro Snax automated evidence disposition, hidden-info draw visibility breadth, activated destroy-self draw breadth, cleanup/replacement duration breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, P0/P1, or READY.
