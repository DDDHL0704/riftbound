# 4D-03KO-E Card Matrix Readiness Payment-Cost Soulsipper Grave-Unit Hidden/Targeting-Stack Blocker Closure Candidate

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-5f679644d4 / OGN·196/298 / 咂魂者 / OGN_SOULSIPPER_GRAVE_UNIT_PLAY_UNIT`.

## Evidence

- `data/official/card-catalog.zh-CN.json` fixed snapshot contains `OGN·196/298` 咂魂者 with Spirit tag and a play trigger that may play a graveyard unit while still paying rune costs.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·196/298` to `OGN_SOULSIPPER_GRAVE_UNIT_PLAY_UNIT`, cost 8, zero targets, `PlaysSourceToBaseAsUnit: true`, source unit power 5, and `SourceUnitTags: "灵体"`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-soulsipper-spirit-static.fixture.json` covers the current hand-play unit path: pay 8, zero-target stack resolution, and source object entering the controller base as a 5-power Spirit unit.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the Soulsipper fixture and direct target rejection for the current zero-target play path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P2_STATUS.md` already record the rule evidence and deferred graveyard unit selection / free play / rune-cost branch boundaries.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 226 -> 225.
- Primary payment-cost residual: 154 -> 153.
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 414 -> 413.
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 140 -> 139.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

## Non-Closure

This candidate does not close Soulsipper automated evidence disposition, graveyard unit selection, free play destination and rune-cost branch, hidden-info / random-zone breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 568/568; Soulsipper focused 3021/3021; adjacent prompt/payment/hidden/target/stack 1895/1895; backend full 5139/5139; git diff --check passed.
