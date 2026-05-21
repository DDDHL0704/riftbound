# 4D-03KI-E Card Matrix Readiness Payment-Cost Miss Fortune No-Optional-Haste Shared-Oracle Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-dcdb944610 / OGN·162/298 + OGN·162a/298 / 厄运小姐 / MISS_FORTUNE_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE;MISS_FORTUNE_PLAY_UNIT_NO_OPTIONAL_HASTE`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `data/official/card-catalog.zh-CN.json` 固定官网快照包含 `OGN·162/298` 与 `OGN·162a/298` 《厄运小姐》，费用 5，战力 5，官方文本包含 `急速`、`游走` 和每回合首次移动时让其他休眠物体变为活跃状态。
- `CardBehaviorRegistry` 已将两个 card no 绑定到 `MISS_FORTUNE_PLAY_UNIT_NO_OPTIONAL_HASTE` 与 `MISS_FORTUNE_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE`，本批不改 runtime。
- `p2-preflight-play-miss-fortune-no-optional-haste.fixture.json` 与 `p2-preflight-play-miss-fortune-alt-a-no-optional-haste.fixture.json` 覆盖基础 5 费、0 目标入栈、结算后进入控制者基地并带 `急速` / `海盗` / `游走` 标签的单位对象。
- `p4-play-miss-fortune-haste-ready.fixture.json` 与 `p4-play-miss-fortune-alt-a-haste-ready.fixture.json` 覆盖当前 HASTE_READY 代表可选费用路径。
- `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md`、`docs/CURRENT_P2_STATUS.md` 与 `docs/CURRENT_P4_STATUS.md` 已记录 accepted representative evidence 和 deferred official breadth。

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 232 -> 231.
- Primary residual: 158 -> 158 because this row remains `SHARED_ORACLE_IMPLEMENTATION`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 420 -> 419.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 142 -> 141.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Miss Fortune automated evidence disposition, precise orange-resource HASTE_READY matching, roam movement breadth, first-move ready/exhausted object trigger, control-zone movement breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

Validation passed:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 556/556.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MissFortune|FullyQualifiedName~MISS_FORTUNE|FullyQualifiedName~HasteReady|FullyQualifiedName~ConformanceFixtureRunnerTests"` passed 3087/3087.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~MissFortune|FullyQualifiedName~MISS_FORTUNE|FullyQualifiedName~HasteReady|FullyQualifiedName~Stack"` passed 720/720.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5127/5127.
- `git diff --check` passed.
