# 4D-03KG-E Audit - Shimmering Aurora

审计结论：本批为 evidence-only matrix readiness slice。`FU-de66a29ed9` 移除 `NEEDS_ENGINE_SUPPORT`，但 `freezeStatus` 保持 `NEEDS_FAQ_REVIEW`，并保留 `NEEDS_FAQ_REVIEW` 与 `NEEDS_AUTOMATED_TEST_EVIDENCE` blockers。不得升级为 `fullOfficial`，不得输出 READY。

接受理由：
- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·160/298`，card name 为《闪耀极光》。
- runtime registry 已存在 `SHIMMERING_AURORA_PLAY_EQUIPMENT` 绑定。
- conformance fixture 已证明基础 9 费、0 目标打出、入栈、结算入基地和装备对象形状。
- target-rejection fixture 已证明显式目标不能被接受，且拒绝不产生支付、移动、入栈或对象创建。
- 规则证据文档已明确 FAQ、回合结束展示、免费打出、hidden/reveal redaction、movement、cleanup/replacement 和 layer 分支暂缓。

本批明确不关闭：
- automated evidence disposition。
- FAQ adjudication。
- end-step reveal branch。
- free-play unit branch。
- hidden-info / reveal redaction breadth。
- control-zone movement breadth。
- cleanup / replacement duration breadth。
- layer / continuous-effect breadth。
- complete PaymentEngine / PAY_COST matrix。
- P0-005、P0-004 adjacency-sensitive audit、P1、E_CARD_MATRIX_READINESS、card matrix、READY。

计数复核：
- snapshot entries = 1009。
- functional units = 811。
- payment-cost functional units = 360。
- payment-cost snapshot entries = 446。
- `NEEDS_ENGINE_SUPPORT`: 234 -> 233。
- primary residual: 158 -> 158。
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 422 -> 421。
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 143 -> 143。
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual = 328。
- `NEEDS_FAQ_REVIEW` residual = 92。
- primary FAQ residual = 61。
- fullOfficialTrue = 0。
- ready = false。

Validation passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 552/552.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shimmering|FullyQualifiedName~Aurora|FullyQualifiedName~SHIMMERING|FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3021/3021.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Shimmering|FullyQualifiedName~Aurora|FullyQualifiedName~SHIMMERING|FullyQualifiedName~Equipment|FullyQualifiedName~Stack"` passed 975/975.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5123/5123.
- `git diff --check` passed.
