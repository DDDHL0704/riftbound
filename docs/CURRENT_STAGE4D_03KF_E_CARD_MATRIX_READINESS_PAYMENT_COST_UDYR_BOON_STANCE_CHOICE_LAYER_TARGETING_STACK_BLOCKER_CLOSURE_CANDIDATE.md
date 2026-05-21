# 4D-03KF-E Card Matrix Readiness - Udyr Boon Stance Choice

结论：`FU-93142f6623` / `OGN·157/298` 《乌迪尔》 / `OGN_UDYR_BOON_STANCE_CHOICE_PLAY_UNIT` 可作为 4D-03KE-E 后的 payment-cost row-level blocker closure candidate。这里只关闭一枚 `NEEDS_ENGINE_SUPPORT` 行级 blocker，不声明 automated evidence disposition、FAQ closure、fullOfficial 或 READY。

证据边界：
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-udyr-boon-stance-static.fixture.json` 已覆盖 6 费、0 目标、入栈、双方让过、结算进入控制者基地并成为 6 战力 `CARD_TYPE:UNIT` 的代表路径。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 绑定该 fixture，并有带目标打出拒绝用例，避免前端或测试绕过服务端目标合法性。
- `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 和 `docs/CURRENT_P2_STATUS.md` 记录该路径的官方目录 / core-rule evidence。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 将 `OGN·157/298` 映射到 `OGN_UDYR_BOON_STANCE_CHOICE_PLAY_UNIT`，本批不改 runtime。

矩阵影响：
- `NEEDS_ENGINE_SUPPORT` functional units: 235 -> 234。
- primary residual: 159 -> 158。
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 423 -> 422。
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 144 -> 143。
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual 保持 328。
- `NEEDS_FAQ_REVIEW` residual 保持 92。
- primary FAQ residual 保持 61。
- `fullOfficialTrue` 保持 0。
- `ready` 保持 false。

非关闭项：
- Udyr automated evidence disposition remains open。
- Udyr boon-consume stance-choice branch remains open。
- Udyr this-turn stance memory remains open。
- Udyr damage / stun / ready / roam branch breadth remains open。
- Udyr cleanup / replacement duration breadth remains open。
- Udyr layer / continuous-effect breadth remains open。
- complete FEPR target / stack lifecycle matrix remains open。
- complete PaymentEngine / PAY_COST matrix remains open。
- formal 18-step E2E remains open。
- READY remains open。

写入范围：
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03KfPaymentCostUdyrBoonStanceChoiceLayerTargetingStackBlockerClosureCandidate`。
- 更新 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 的 Post03Kf manifest、count assertions、doc-anchor aggregation 和 audit mapping assertions。
- 更新当前 checkpoint / audit 文档。
- 不修改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status 或 final readiness flags。

Validation complete: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; Udyr/UDYR/Boon/Stance/ConformanceFixtureRunnerTests focused regression 3053/3053 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/Udyr/UDYR/Boon/Stance/Stack adjacent regression 717/717 passed; PaymentEngineCoverageAuditTests 550/550 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5121/5121 passed; `git diff --check` passed after final doc write.
