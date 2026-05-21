4D-03LV-E payment-cost Brutalizer equipment FAQ/layer/control blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03LU-E 后的第一百六十八枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03LvPaymentCostBrutalizerEquipmentFaqLayerControlBlockerClosureCandidate`。`Post03LvCardMatrixReadinessPaymentCostBrutalizerEquipmentFaqLayerControlBlockerClosureCandidateManifest` records selected functionalUnit=FU-07d3ef1984；selected card=SFD·042/221 残暴之力；selected effect=BRUTALIZER_PLAY_EQUIPMENT；NEEDS_ENGINE_SUPPORT 193 -> 192；primary residual 136 -> 136；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 380 -> 379；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 121 -> 121；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Brutalizer automated evidence disposition、FAQ adjudication、complete equipment / attach lifecycle breadth、LayerEngine / continuous-effect breadth、ZoneOwnership / control-zone movement breadth、complete PaymentEngine / PAY_COST matrix 与 formal 18-step E2E 仍 open；Chrome smoke not run for 03LV because there were no frontend or browser-script changes；validation passed for 4D-03LV-E: matrix JSON valid (jq empty); 03LV matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 613/613; Brutalizer focused regression 6/6; adjacent prompt/payment/equipment/layer/control regression 2460/2460; backend full test 5184/5184; git diff --check passed.

# 4D-03LV-E card matrix readiness slice

## Selected row

- functionalUnitId: `FU-07d3ef1984`
- card: `SFD·042/221` / 残暴之力
- effect: `BRUTALIZER_PLAY_EQUIPMENT`
- selected matrix row: `payment-cost`
- secondary row query: `payment-cost-layer-continuous-control-zone`

## Evidence accepted for this slice

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the fixed official card/effect row for 残暴之力.
- `src/Riftbound.Engine/MatchSession.cs` and `src/Riftbound.Engine/CoreRuleEngine.cs` contain the server-authored Brutalizer equipment profile and assemble path used by this evidence row.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-brutalizer-equipment.fixture.json` covers the zero-target equipment hand-play path into base.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-brutalizer-target-rejected.fixture.json` covers explicit-target rejection for the zero-target equipment play path.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` records the Brutalizer play, target-rejection and assemble representative evidence.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, `docs/CURRENT_P4_STATUS.md`, `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`, and `docs/CURRENT_SERVER_RULE_AUDIT.md` record rule-audited evidence for the equipment play / assemble representative path.

## Matrix transition

- `NEEDS_ENGINE_SUPPORT`: `193 -> 192`
- primary residual: `136 -> 136`
- `payment-or-targeting-stack-timing`: `380 -> 379`
- `payment-and-targeting-stack-timing`: `121 -> 121`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains `328`
- `NEEDS_FAQ_REVIEW`: remains `92`
- `fullOfficialTrue`: remains `0`
- final readiness: remains `false`

## Non-closure

This slice does not close Brutalizer automated evidence disposition, FAQ adjudication, complete equipment / attach lifecycle breadth, LayerEngine / continuous-effect breadth, ZoneOwnership / control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, P0/P1, or READY.
