# 4D-03KR-E Card Matrix Readiness Payment-Cost Rhasa Full-Cost Spirit FAQ Hidden Blocker Closure Candidate

本批只处理 `FU-d4b4d9af72` / `OGN·195/298`《裂魂者喇煞》 / `RHASA_FULL_COST_SPIRIT_PLAY_UNIT` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker disposition。

Accepted evidence:

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-rhasa-full-cost-spirit-unit.fixture.json`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `data/official/card-catalog.zh-CN.json`

Closure scope:

- Empty-graveyard full 10-cost hand play is represented.
- Zero-target stack resolution is represented.
- Source-to-base unit object is represented.
- Final unit power 6 and Spirit tag are represented.
- Direct target rejection is represented by the existing conformance runner path.

Matrix impact:

- `NEEDS_ENGINE_SUPPORT 223 -> 222`
- `primary residual 151 -> 151`
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 411 -> 410`
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 138 -> 138`
- `NEEDS_AUTOMATED_TEST_EVIDENCE residual=328`
- `NEEDS_FAQ_REVIEW residual=92`
- `primary FAQ residual=61`
- `fullOfficialTrue 0 -> 0`
- `ready false -> false`

Non-closure:

- Rhasa automated evidence disposition remains open.
- Rhasa FAQ adjudication remains open.
- Rhasa graveyard-count cost reduction remains open.
- Rhasa hidden-info / random-zone breadth remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- Full official PaymentEngine matrix closure remains open.
- E_CARD_MATRIX_READINESS remains open.
- READY remains open.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 574/574; Rhasa focused 3021/3021; adjacent prompt/payment/FAQ/hidden 374/374; backend full 5145/5145; git diff --check passed.
