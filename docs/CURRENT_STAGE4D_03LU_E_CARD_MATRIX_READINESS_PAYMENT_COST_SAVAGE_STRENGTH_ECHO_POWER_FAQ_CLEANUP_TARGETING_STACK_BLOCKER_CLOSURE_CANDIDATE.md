4D-03LU-E payment-cost Savage Strength echo-power FAQ/cleanup/targeting-stack blocker closure candidate 已建立：E_CARD_MATRIX_READINESS 已把 4D-03LT-E 后的第一百六十七枚 row-level blocker-count reduction 落入 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03LuPaymentCostSavageStrengthEchoPowerFaqCleanupTargetingStackBlockerClosureCandidate`。`Post03LuCardMatrixReadinessPaymentCostSavageStrengthEchoPowerFaqCleanupTargetingStackBlockerClosureCandidateManifest` records selected functionalUnit=FU-ff59e9b029；selected card=SFD·034/221 蛮荒之力；selected effect=SAVAGE_STRENGTH_POWER_PLUS_2；NEEDS_ENGINE_SUPPORT 194 -> 193；primary residual 136 -> 136；payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 381 -> 380；payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 122 -> 121；NEEDS_AUTOMATED_TEST_EVIDENCE residual=328；NEEDS_FAQ_REVIEW residual=92；primary FAQ residual=61；fullOfficialTrue 0 -> 0；ready false -> false；项目仍 **NOT READY**。本批不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status、FAQ status 或 final readiness flags；Savage Strength automated evidence disposition、FAQ adjudication、complete Echo optional-cost/repeat breadth、until-end-of-turn power modifier cleanup breadth、complete FEPR target / stack lifecycle matrix、complete PaymentEngine / PAY_COST matrix 与 formal 18-step E2E 仍 open；Chrome smoke not run for 03LU because there were no frontend or browser-script changes；validation passed for 4D-03LU-E: matrix JSON valid (jq empty); 03LU matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 611/611; Savage Strength focused regression 8/8; adjacent prompt/payment/echo/stack/cleanup/replacement regression 1995/1995; backend full test 5182/5182; git diff --check passed.

# 4D-03LU-E card matrix readiness slice

## Selected row

- functionalUnitId: `FU-ff59e9b029`
- card: `SFD·034/221` / 蛮荒之力
- effect: `SAVAGE_STRENGTH_POWER_PLUS_2`
- selected matrix row: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

## Evidence accepted for this slice

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the fixed official card/effect row for 蛮荒之力.
- `src/Riftbound.Engine/MatchSession.cs` contains the server-authored play/stack handling used by this evidence row.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-savage-strength-echo-power-stack.fixture.json` covers paying Echo and repeating the until-end-of-turn +2 power modifier.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` and `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` keep the authoritative play/submit regression coverage attached to the official card number.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P7_9_STATUS.md` keep the prior rules-evidence trace for this implementation path.

## Matrix transition

- `NEEDS_ENGINE_SUPPORT`: `194 -> 193`
- primary residual: `136 -> 136`
- `payment-or-targeting-stack-timing`: `381 -> 380`
- `payment-and-targeting-stack-timing`: `122 -> 121`
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains `328`
- `NEEDS_FAQ_REVIEW`: remains `92`
- `fullOfficialTrue`: remains `0`
- final readiness: remains `false`

## Non-closure

This slice does not close Savage Strength automated evidence disposition, FAQ adjudication, complete Echo optional-cost/repeat breadth, complete until-end-of-turn modifier cleanup breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, P0/P1, or READY.
