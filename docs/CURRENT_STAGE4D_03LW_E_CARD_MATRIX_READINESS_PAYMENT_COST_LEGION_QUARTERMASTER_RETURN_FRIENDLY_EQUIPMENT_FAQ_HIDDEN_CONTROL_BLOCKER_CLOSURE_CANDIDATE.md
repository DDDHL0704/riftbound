4D-03LW-E payment-cost Legion Quartermaster return-friendly-equipment FAQ/hidden/control blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03LV-E 后的第一百六十九枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03LwPaymentCostLegionQuartermasterReturnFriendlyEquipmentFaqHiddenControlBlockerClosureCandidate`。`Post03LwCardMatrixReadinessPaymentCostLegionQuartermasterReturnFriendlyEquipmentFaqHiddenControlBlockerClosureCandidateManifest` records selected functionalUnit=FU-ae03379f19；selected card=SFD·044/221 军团军需官；selected effect=LEGION_QUARTERMASTER_RETURN_FRIENDLY_EQUIPMENT_STATIC；NEEDS_ENGINE_SUPPORT 192 -> 191；primary residual 136 -> 136；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 379 -> 378；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 121 -> 121；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Legion Quartermaster automated evidence disposition、FAQ adjudication、hidden-info / equipment-return visibility breadth、control-zone movement / object-location breadth、complete PaymentEngine / PAY_COST matrix 与 formal 18-step E2E 仍 open；Chrome smoke not run for 03LW because there were no frontend or browser-script changes；matrix JSON valid (jq empty); 03LW matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 615/615; Legion Quartermaster focused regression 6/6; adjacent prompt/payment/return-friendly-equipment/equipment/hidden/control regression 2447/2447; backend full test 5186/5186; git diff --check passed.

# 4D-03LW-E card matrix readiness slice

## Selected row

- functionalUnitId: `FU-ae03379f19`
- card: `SFD·044/221` / 军团军需官
- effect: `LEGION_QUARTERMASTER_RETURN_FRIENDLY_EQUIPMENT_STATIC`
- selected matrix row: `payment-cost`
- secondary row query: `payment-cost-control-zone-hidden-info`

## Evidence accepted for this slice

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the fixed official card/effect row for 军团军需官.
- `src/Riftbound.Engine/CoreRuleEngine.cs` contains the server-authored `RETURN_FRIENDLY_EQUIPMENT:<objectId>` additional-cost parser, validation and return-to-hand event path used by this evidence row.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-legion-quartermaster-return-friendly-equipment.fixture.json` covers returning a friendly equipment as the mandatory additional cost, zero-target stack resolution, and unit entry to base.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the accepted path plus missing cost, unit-target and enemy-equipment rejection paths.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P2_STATUS.md` record rule-audited evidence for the return-friendly-equipment additional-cost path.

## Matrix transition

- `NEEDS_ENGINE_SUPPORT`: `192 -> 191`
- primary residual: `136 -> 136`
- `payment-or-targeting-stack-timing`: `379 -> 378`
- `payment-and-targeting-stack-timing`: `121 -> 121`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains `328`
- `NEEDS_FAQ_REVIEW`: remains `92`
- `fullOfficialTrue`: remains `0`
- final readiness: remains `false`

## Non-closure

This slice does not close Legion Quartermaster automated evidence disposition, FAQ adjudication, hidden-info / equipment-return visibility breadth, control-zone movement / object-location breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, P0/P1, or READY.
