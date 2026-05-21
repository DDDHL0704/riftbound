# 4D-03KQ-E Card Matrix Readiness Payment-Cost Glory Call Power Layer Targeting-Stack Blocker Closure Candidate

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-d2ae717e65 / OGN·207/298 / 荣耀召唤 / GLORY_CALL_POWER_PLUS_3`.

## Evidence

- `data/official/card-catalog.zh-CN.json` records OGN·207/298 as a 3-cost reaction spell: it may consume a boon as an additional cost to ignore this spell's cost, then gives one unit +3 power this turn.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·207/298` to `GLORY_CALL_POWER_PLUS_3`, cost 3, one any-unit target and +3 power modifier.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-glory-call-power-plus-3.fixture.json` covers the base non-boon-consume path: pay 3, add stack item, pass priority, resolve, apply +3 until-end-of-turn power modifier and move the source spell to graveyard.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the fixture through `CoreRuleEnginePlaysGloryCallPowerBoost`.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` already cite the official catalog and core-rules timing/cleanup evidence for this representative path.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 224 -> 223.
- Primary payment-cost residual: 152 -> 151.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 412 -> 411.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 139 -> 138.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328.
- `NEEDS_FAQ_REVIEW`: remains 92.
- `fullOfficialTrue`: remains 0.
- `ready`: remains false.

This candidate does not close Glory Call automated evidence disposition, consume-boon alternate-cost no-mana branch, until-end-of-turn cleanup / replacement duration, layer / continuous-effect breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 572/572; Glory Call focused 3021/3021; adjacent prompt/payment/power/layer/target/stack 2191/2191; backend full 5143/5143; git diff --check passed.
