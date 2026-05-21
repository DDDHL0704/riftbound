# 4D-03KI-E Audit - Miss Fortune No-Optional-Haste Shared Oracle

本审计记录 4D-03KI-E 对 `FU-dcdb944610` / `OGN·162/298` + `OGN·162a/298` 《厄运小姐》 / `MISS_FORTUNE_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE;MISS_FORTUNE_PLAY_UNIT_NO_OPTIONAL_HASTE` 的 row-level blocker closure candidate。结论限定为：已有基础打出、支付、0 目标、单位对象、HASTE_READY 代表路径和官方目录证据足以移除一枚 `NEEDS_ENGINE_SUPPORT` blocker；完整 automated evidence disposition、橙色资源精确匹配、游走移动、首次移动唤醒其他休眠物体、control-zone movement、FEPR 和 full PaymentEngine 仍 open。

## Source Evidence

- 官方目录行存在于 `data/official/card-catalog.zh-CN.json`，card no 为 `OGN·162/298` 与 `OGN·162a/298`，card name 为《厄运小姐》。
- runtime registry 已存在 `MISS_FORTUNE_PLAY_UNIT_NO_OPTIONAL_HASTE` 与 `MISS_FORTUNE_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE` 绑定。
- P2 preflight fixtures 覆盖基础 5 费、0 目标入栈、双方让过、源牌进入控制者基地、单位对象战力 5、`CARD_TYPE:UNIT|急速|海盗|游走` 标签。
- P4 HASTE_READY fixtures 覆盖当前代表可选费用路径并记录 `hasteReadyOptionalCostPaid: true` 与活跃入场 payload。
- `rules-evidence-index.md`、`p2-rules-preflight.md`、`CURRENT_P2_STATUS.md` 与 `CURRENT_P4_STATUS.md` 已将该 evidence 绑定到 catalog 与核心规则。

## Accepted Closure

- 仅接受 `NEEDS_ENGINE_SUPPORT 232 -> 231` 的行级 reduction。
- 仅接受 `payment-or-targeting-stack-timing 420 -> 419` 和 `payment-and-targeting-stack-timing 142 -> 141` 的派生 row count reduction。
- `freezeStatus` 保持 `SHARED_ORACLE_IMPLEMENTATION`。
- `fullOfficialBlockers` 从 `NEEDS_ENGINE_SUPPORT+NEEDS_AUTOMATED_TEST_EVIDENCE` 变为 `NEEDS_AUTOMATED_TEST_EVIDENCE`。
- `statusFlags` 从 `IMPLEMENTED_UNTESTED+SHARED_ORACLE_IMPLEMENTATION+NEEDS_ENGINE_SUPPORT` 变为 `IMPLEMENTED_UNTESTED+SHARED_ORACLE_IMPLEMENTATION`。

## Rejected Closure

- 不关闭 automated evidence disposition。
- 不关闭 precise orange-resource HASTE_READY matching。
- 不关闭 roam movement breadth。
- 不关闭 first-move ready/exhausted object trigger。
- 不关闭 control-zone movement breadth。
- 不关闭 complete FEPR target / stack lifecycle matrix。
- 不关闭 complete PaymentEngine / PAY_COST matrix。
- 不改变 fullOfficial 或 READY。

## Validation

Validation passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 556/556.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MissFortune|FullyQualifiedName~MISS_FORTUNE|FullyQualifiedName~HasteReady|FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3087/3087.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~MissFortune|FullyQualifiedName~MISS_FORTUNE|FullyQualifiedName~HasteReady|FullyQualifiedName~Stack"` passed 720/720.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5127/5127.
- `git diff --check` passed.
