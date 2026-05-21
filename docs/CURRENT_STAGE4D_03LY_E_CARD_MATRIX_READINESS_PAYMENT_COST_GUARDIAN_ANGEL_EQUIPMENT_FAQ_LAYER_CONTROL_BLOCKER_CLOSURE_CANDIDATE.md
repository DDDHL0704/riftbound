4D-03LY-E payment-cost Guardian Angel equipment FAQ/layer/control blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03LX-E 后的第一百七十一枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03LyPaymentCostGuardianAngelEquipmentFaqLayerControlBlockerClosureCandidate`。`Post03LyCardMatrixReadinessPaymentCostGuardianAngelEquipmentFaqLayerControlBlockerClosureCandidateManifest` records selected functionalUnit=FU-fbb97dc234；selected card=SFD·051/221 守护天使；selected effect=GUARDIAN_ANGEL_PLAY_EQUIPMENT；NEEDS_ENGINE_SUPPORT 190 -> 189；primary residual 135 -> 135；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 377 -> 376；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 121 -> 121；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Guardian Angel automated evidence disposition、FAQ adjudication、replacement text breadth、complete equipment / attach lifecycle breadth、LayerEngine / continuous-effect breadth、ZoneOwnership / control-zone movement breadth、complete PaymentEngine / PAY_COST matrix 与 formal 18-step E2E 仍 open；Chrome smoke not run for 03LY because there were no frontend or browser-script changes；matrix JSON valid (jq empty); 03LY matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 619/619; Guardian Angel focused regression 6/6; adjacent prompt/payment/equipment/assemble/layer/control regression 2488/2488; backend full test 5190/5190; git diff --check passed.

# 4D-03LY-E card matrix readiness slice

## Selected row

- functionalUnitId: `FU-fbb97dc234`
- card: `SFD·051/221` / 守护天使
- effect: `GUARDIAN_ANGEL_PLAY_EQUIPMENT`
- selected matrix row: `payment-cost`
- secondary row query: `payment-cost-equipment-faq-layer-control`

## Evidence accepted for this slice

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the fixed official card/effect row for 守护天使 with zero-target equipment play behavior.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-guardian-angel-equipment.fixture.json` covers paying 2, creating the zero-target stack item, and resolving the source as a base equipment object.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-guardian-angel-target-rejected.fixture.json` covers rejecting an explicit target before payment, zone movement, equipment entry, or stack creation.
- `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/MatchSession.cs`, and conformance tests record the existing `ASSEMBLE_GREEN` representative attach path for 守护天使.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, and `docs/CURRENT_P4_STATUS.md` record rule-audited evidence for the Guardian Angel equipment entry, target rejection, and representative attach paths.

## Matrix transition

- `NEEDS_ENGINE_SUPPORT`: `190 -> 189`
- primary residual: `135 -> 135`
- `payment-or-targeting-stack-timing`: `377 -> 376`
- `payment-and-targeting-stack-timing`: `121 -> 121`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains `328`
- `NEEDS_FAQ_REVIEW`: remains `92`
- `fullOfficialTrue`: remains `0`
- final readiness: remains `false`

## Non-closure

This slice does not close Guardian Angel automated evidence disposition, FAQ adjudication, replacement text breadth, complete equipment / attach lifecycle breadth, LayerEngine / continuous-effect breadth, ZoneOwnership / control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, P0/P1, or READY.
