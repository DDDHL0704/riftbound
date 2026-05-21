# 4D-03KG-E Card Matrix Readiness - Shimmering Aurora

结论：`FU-de66a29ed9` / `OGN·160/298` 《闪耀极光》 / `SHIMMERING_AURORA_PLAY_EQUIPMENT` 可作为 4D-03KF-E 后的 payment-cost row-level blocker closure candidate。这里只关闭一枚 `NEEDS_ENGINE_SUPPORT` 行级 blocker；`NEEDS_FAQ_REVIEW`、`NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 和 READY 均保持不变。

证据边界：
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-shimmering-aurora-equipment.fixture.json` 已覆盖 9 费、0 目标、入栈、双方让过、结算进入控制者基地并成为 `CARD_TYPE:EQUIPMENT` 装备对象的代表路径。
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-shimmering-aurora-target-rejected.fixture.json` 已覆盖显式目标打出拒绝，拒绝后不支付费用、不移动手牌、不入栈、不创建装备对象。
- `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 和 `docs/CURRENT_P2_STATUS.md` 记录该路径的 catalog / core-rule evidence，并明确回合结束展示与免费打出分支暂缓。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已将 `OGN·160/298` 绑定到 `SHIMMERING_AURORA_PLAY_EQUIPMENT`，本批不改 runtime。

矩阵影响：
- `NEEDS_ENGINE_SUPPORT` functional units: 234 -> 233。
- primary residual: 158 -> 158。
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 422 -> 421。
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 143 -> 143。
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual 保持 328。
- `NEEDS_FAQ_REVIEW` residual 保持 92。
- primary FAQ residual 保持 61。
- `fullOfficialTrue` 保持 0。
- `ready` 保持 false。

非关闭项：
- Shimmering Aurora automated evidence disposition remains open。
- Shimmering Aurora FAQ adjudication remains open。
- Shimmering Aurora end-step reveal branch remains open。
- Shimmering Aurora free-play unit branch remains open。
- Shimmering Aurora hidden-info / reveal redaction breadth remains open。
- Shimmering Aurora control-zone movement breadth remains open。
- Shimmering Aurora cleanup / replacement duration breadth remains open。
- Shimmering Aurora layer / continuous-effect breadth remains open。
- complete PaymentEngine / PAY_COST matrix remains open。
- formal 18-step E2E remains open。
- READY remains open。

写入范围：
- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03KgPaymentCostShimmeringAuroraEquipmentFaqHiddenLayerBlockerClosureCandidate`。
- 更新 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 的 Post03Kg manifest、count assertions、doc-anchor aggregation 和 audit mapping assertions。
- 更新当前 checkpoint / audit 文档。
- 不修改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status 或 final readiness flags。

Validation passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 552/552.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shimmering|FullyQualifiedName~Aurora|FullyQualifiedName~SHIMMERING|FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3021/3021.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Shimmering|FullyQualifiedName~Aurora|FullyQualifiedName~SHIMMERING|FullyQualifiedName~Equipment|FullyQualifiedName~Stack"` passed 975/975.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5123/5123.
- `git diff --check` passed.
