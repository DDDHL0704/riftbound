# 4D-03KN-E Card Matrix Readiness Payment-Cost Nocturne Roam Hidden/Layer/Control Targeting-Stack Blocker Closure Candidate

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-4db1229ebc / OGN·194/298 / 魔腾 / NOCTURNE_ROAM_PLAY_UNIT`.

## Evidence

- `data/official/card-catalog.zh-CN.json` fixed snapshot contains `OGN·194/298` 魔腾 with Roam and a top-deck-look alternate-play payment branch.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds `OGN·194/298` to `NOCTURNE_ROAM_PLAY_UNIT`, cost 4, zero targets, `PlaysSourceToBaseAsUnit: true`, source unit power 4, and `SourceUnitTags: "游走"`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-nocturne-roam-keyword-unit.fixture.json` covers the current hand-play unit path: pay 4, zero-target stack resolution, and source object entering the controller base as a 4-power unit with the Roam tag.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the Nocturne fixture and direct target rejection for the current zero-target play path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P2_STATUS.md` already record the rule evidence and deferred roam movement / top-deck alternate-play boundaries.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 227 -> 226.
- Primary payment-cost residual: 155 -> 154.
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 415 -> 414.
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 141 -> 140.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

## Non-Closure

This candidate does not close Nocturne automated evidence disposition, roam movement breadth, top-deck look / alternate-play payment branch, hidden-info / random-zone breadth, layer / continuous-effect breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation

Validation passed: matrix JSON valid; PaymentEngineCoverageAuditTests 566/566; Nocturne focused 3021/3021; adjacent prompt/payment/roam/target/stack 1882/1882; backend full 5137/5137; git diff --check passed.
