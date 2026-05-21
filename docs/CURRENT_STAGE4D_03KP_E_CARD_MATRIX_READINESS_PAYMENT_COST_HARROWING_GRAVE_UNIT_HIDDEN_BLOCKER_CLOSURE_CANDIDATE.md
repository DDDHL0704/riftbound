# 4D-03KP-E Card Matrix Readiness Payment-Cost Harrowing Grave-Unit Hidden Blocker Closure Candidate

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-c21b09595c / OGN·198/298 / 蚀魂夜 / HARROWING_PLAY_GRAVEYARD_UNIT_TO_BASE`.

## Evidence

- `data/official/card-catalog.zh-CN.json` fixed snapshot contains `OGN·198/298` 蚀魂夜 with cost 6 and text that plays a unit from the controller graveyard while ignoring mana cost and still requiring rune costs.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·198/298` to `HARROWING_PLAY_GRAVEYARD_UNIT_TO_BASE`, cost 6, one target, and graveyard-unit-to-base resolution behavior.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-harrowing-play-graveyard-unit-base.fixture.json` covers the current representative path: pay 6, select a friendly graveyard unit target, add the stack item, pass priority, and play that graveyard unit to the controller base.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the Harrowing fixture, non-unit graveyard target rejection, and the guard that skips an opponent-controlled dirty target in the controller graveyard.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_SERVER_RULE_AUDIT.md`, and `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` already record the rule evidence and deferred complete destination / rune-cost / hidden-info boundaries.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 225 -> 224.
- Primary payment-cost residual: 153 -> 152.
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 413 -> 412.
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 139 -> 139.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

## Non-Closure

This candidate does not close Harrowing automated evidence disposition, complete destination selection, rune-cost branch, hidden-info / random-zone breadth, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 570/570; Harrowing focused 3021/3021; adjacent prompt/payment/graveyard/hidden/stack 733/733; backend full 5141/5141; git diff --check passed.
