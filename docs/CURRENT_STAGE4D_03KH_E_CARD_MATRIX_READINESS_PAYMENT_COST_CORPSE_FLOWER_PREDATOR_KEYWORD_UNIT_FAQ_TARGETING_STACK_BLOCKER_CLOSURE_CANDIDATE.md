# 4D-03KH-E Card Matrix Readiness - Corpse Flower Predator

结论：`FU-6a8c269c38` / `OGN·161/298` 《亡花掠食者》 / `CORPSE_FLOWER_PREDATOR_PLAY_KEYWORD_UNIT` 可作为 4D-03KG-E 后的 payment-cost row-level blocker closure candidate。这里只关闭一枚 `NEEDS_ENGINE_SUPPORT` 行级 blocker；`NEEDS_FAQ_REVIEW`、`NEEDS_AUTOMATED_TEST_EVIDENCE`、`fullOfficial=false` 和 READY 均保持不变。

## Evidence

- `data/official/card-catalog.zh-CN.json` 固定官网快照包含 `OGN·161/298` 《亡花掠食者》，费用 8，战力 8，官方文本包含 `法盾` 和可打出到敌方控制战场。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 已将 `OGN·161/298` 绑定到 `CORPSE_FLOWER_PREDATOR_PLAY_KEYWORD_UNIT`，本批不改 runtime。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-corpse-flower-predator-keyword-unit.fixture.json` 覆盖基础 8 费、0 目标入栈、结算后进入控制者基地并带 `法盾` 标签的单位对象。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 引用该 fixture 并检查 `CARD_TYPE:UNIT|法盾`。
- `docs/rules-evidence-index.md` 与 `docs/p2-rules-preflight.md` 已记录该 preflight 证据及暂缓范围。

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`：233 -> 232。
- primary residual：158 -> 158。
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`：421 -> 420。
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`：143 -> 142。
- `NEEDS_AUTOMATED_TEST_EVIDENCE`：328 -> 328。
- `NEEDS_FAQ_REVIEW`：92 -> 92。
- `fullOfficialTrue`：0 -> 0。
- `ready`：false -> false。

## Non-Closure

- Corpse Flower Predator automated evidence disposition remains open。
- Corpse Flower Predator FAQ adjudication remains open。
- Corpse Flower Predator Spellshield target-tax branch remains open。
- Corpse Flower Predator enemy-controlled battlefield placement branch remains open。
- Corpse Flower Predator control-zone movement breadth remains open。
- complete FEPR target / stack lifecycle matrix remains open。
- complete PaymentEngine / PAY_COST matrix remains open。
- fullOfficial remains false。
- READY remains open。

## Write Scope

- 更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 顶层 `stage4D03KhPaymentCostCorpseFlowerPredatorKeywordUnitFaqTargetingStackBlockerClosureCandidate`。
- 更新 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 的 Post03Kh manifest、count assertions、doc-anchor aggregation 和 audit mapping assertions。
- 更新 A 当前 checkpoint / current audit docs。
- 不改 runtime、frontend、Chrome/browser script、official catalog、protocol core fields、fullOfficial status 或 final readiness flags。

Validation passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 554/554.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CorpseFlower|FullyQualifiedName~CORPSE_FLOWER|FullyQualifiedName~Predator|FullyQualifiedName~亡花|FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3021/3021.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~CorpseFlower|FullyQualifiedName~CORPSE_FLOWER|FullyQualifiedName~Predator|FullyQualifiedName~Spellshield|FullyQualifiedName~Target|FullyQualifiedName~Stack"` passed 1880/1880.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5125/5125.
- `git diff --check` passed.
