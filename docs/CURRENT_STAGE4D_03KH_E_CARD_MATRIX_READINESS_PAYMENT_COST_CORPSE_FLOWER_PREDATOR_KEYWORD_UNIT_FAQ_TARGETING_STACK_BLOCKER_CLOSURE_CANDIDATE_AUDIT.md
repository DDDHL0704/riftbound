# 4D-03KH-E Audit - Corpse Flower Predator

本审计记录 4D-03KH-E 对 `FU-6a8c269c38` / `OGN·161/298` 《亡花掠食者》 / `CORPSE_FLOWER_PREDATOR_PLAY_KEYWORD_UNIT` 的 row-level blocker closure candidate。结论限定为：已有基础打出、支付、0 目标、单位对象、法盾标签和官方目录证据足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 FAQ adjudication、法盾目标税、敌方控制战场打出、control-zone movement、FEPR 和 full PaymentEngine 仍 open。

## Source Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·161/298`，card name 为《亡花掠食者》。
- runtime registry 已存在 `CORPSE_FLOWER_PREDATOR_PLAY_KEYWORD_UNIT` 绑定。
- `p2-preflight-play-corpse-flower-predator-keyword-unit.fixture.json` 覆盖基础 8 费、0 目标入栈、双方让过、源牌进入控制者基地、单位对象战力 8、`CARD_TYPE:UNIT|法盾` 标签。
- `ConformanceFixtureRunnerTests` 对该 fixture 有 inline coverage。
- `rules-evidence-index.md` 与 `p2-rules-preflight.md` 已将该 evidence 绑定到 catalog 与核心规则。

## Accepted Closure

- 仅接受 `NEEDS_ENGINE_SUPPORT 233 -> 232` 的行级 reduction。
- 仅接受 `payment-or-targeting-stack-timing 421 -> 420` 和 `payment-and-targeting-stack-timing 143 -> 142` 的派生 row count reduction。
- `freezeStatus` 保持 `NEEDS_FAQ_REVIEW`。
- `fullOfficialBlockers` 从 `NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE` 变为 `NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE`。
- `statusFlags` 从 `IMPLEMENTED_UNTESTED+NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW` 变为 `IMPLEMENTED_UNTESTED+NEEDS_FAQ_REVIEW`。

## Rejected Closure

- 不关闭 automated evidence disposition。
- 不关闭 FAQ adjudication。
- 不关闭 Spellshield target-tax branch。
- 不关闭 enemy-controlled battlefield placement branch。
- 不关闭 control-zone movement breadth。
- 不关闭 complete FEPR target / stack lifecycle matrix。
- 不关闭 complete PaymentEngine / PAY_COST matrix。
- 不改变 fullOfficial 或 READY。

## Validation

Validation passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 554/554.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CorpseFlower|FullyQualifiedName~CORPSE_FLOWER|FullyQualifiedName~Predator|FullyQualifiedName~亡花|FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3021/3021.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~CorpseFlower|FullyQualifiedName~CORPSE_FLOWER|FullyQualifiedName~Predator|FullyQualifiedName~Spellshield|FullyQualifiedName~Target|FullyQualifiedName~Stack"` passed 1880/1880.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5125/5125.
- `git diff --check` passed.
