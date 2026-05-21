# 4D-03KS-E Card Matrix Readiness Payment-Cost Cold-Blooded Aristocrat Destroy-Friendly-Unit FAQ Cleanup Blocker Closure Candidate

本批只处理 `FU-a597c5db86` / `OGN·208/298`《冷血贵族》 / `COLD_BLOODED_ARISTOCRAT_DESTROY_FRIENDLY_UNIT_STATIC` 的 row-level `NEEDS_ENGINE_SUPPORT` blocker disposition。

已接受的服务端代表路径：

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-cold-blooded-aristocrat-destroy-friendly-unit.fixture.json` 覆盖从手牌支付 4 点费用打出。
- `optionalCosts = ["DESTROY_FRIENDLY_UNIT:<objectId>"]` 覆盖摧毁一名友方单位作为强制额外费用。
- 结算链为 0 目标；双方让过后源牌进入控制者基地。
- 源牌成为 6 战力 `CARD_TYPE:UNIT` 单位对象。
- `ConformanceFixtureRunnerTests` 直接拒绝缺少额外费用和以装备作为摧毁费用目标。

矩阵影响：

- `NEEDS_ENGINE_SUPPORT`: 222 -> 221.
- primary residual: 151 -> 151.
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 410 -> 409.
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 138 -> 138.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

本批没有改变：

- runtime / protocol / frontend / Chrome 或 browser script.
- `data/official/card-catalog.zh-CN.json`.
- `fullOfficial` / READY / READY-CANDIDATE.
- 非选中矩阵行。

仍然 open：

- Cold-Blooded Aristocrat automated evidence disposition remains open.
- Cold-Blooded Aristocrat FAQ adjudication remains open.
- Cold-Blooded Aristocrat cleanup / replacement-duration breadth remains open.
- complete mandatory additional-cost PaymentEngine / PAY_COST matrix remains open.
- complete PaymentEngine / PAY_COST matrix remains open.
- formal 18-step E2E remains open.
- READY remains open.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 576/576; Cold-Blooded Aristocrat focused 3021/3021; adjacent prompt/payment/FAQ/cleanup 410/410; backend full 5147/5147; git diff --check passed.
