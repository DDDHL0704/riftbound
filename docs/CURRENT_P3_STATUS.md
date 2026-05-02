# Current P3 Status

更新时间：2026-05-02

这是 P3 卡牌数据与行为系统的短状态文件。P2 core rules preflight 完成状态仍以 `docs/CURRENT_P2_STATUS.md` 为准；P2.5 开发期测试 UI 状态仍以 `docs/CURRENT_P2_5_STATUS.md` 为准。

## Goal

完成 P3 卡牌数据与行为系统：官方卡牌 schema 校验、`811` 个 functional units 稳定输出、每张官方牌可展示 `BehaviorSpec` 状态、规则文本解析最小管线、模板执行器骨架，以及对应测试、文档和状态同步。

## Scope

本阶段要做：

- 保持 `data/official/card-catalog.zh-CN.json` 作为官方快照输入，`1009` 条卡牌必须可导入并通过 schema 校验。
- 保持 `FunctionalUnitBuilder` 输出 `811` 个功能逻辑单元，且 unit id 必须确定性、唯一、稳定。
- 新增 `BehaviorSpec` / `TriggerSpec` / `ReplacementSpec` / `ActivatedAbilitySpec` / `StaticAbilitySpec` 模型。
- 新增 keyword / cost / target / trigger / effect phrase 规则文本解析最小管线。
- 每张官方牌生成明确状态：`implemented`、`manual-rule-required` 或 `unimplemented`，并带 reason。
- 新增模板执行器骨架与 registry：`draw`、`damage`、`destroy`、`move` / `recall`、`stun`、`temp might`、`gain experience`、`assemble`、`echo`、`ambush`。
- 只记录少量安全的既有 P2 手写行为映射，不替换 `CoreRuleEngine` / `CardBehaviorRegistry` 的现有权威结算路径。

本阶段明确不做：

- 不进入 P4 高频关键词大规模实现。
- 不进入 P6 全卡牌批量规则接入。
- 不进入 P7 最终产品 UI。
- 不提交规则 PDF/FAQ。
- 不提交未跟踪的 `riftbound-dotnet.sln`。

## Current Baseline

- 官方快照：`data/official/card-catalog.zh-CN.json`
- 快照日期：`2026-04-27`
- 官方条目：`1009`
- 当前 functional units：`811`
- P2 core rules preflight：`811/811 = 100.0%`
- 最小 card behavior registry：`794/811 = 97.9%`
- 可打出官方牌差集：已清空
- 最新提交：`216a2ae feat: complete p2.5 dev test bench`

## Implementation Plan

| Part | Status | Notes |
|---|---|---|
| P3.0 审计与状态文档 | In Progress | 建立本文件，确认 P3 不回退 P2/P2.5。 |
| P3.1 BehaviorSpec contracts/model | Pending | 放在稳定 DTO/模型层，供 catalog、API、engine skeleton 和测试消费。 |
| P3.2 schema 校验与 functional unit stable report | Pending | 覆盖 `1009` 卡和 `811` units。 |
| P3.3 规则文本解析最小管线 | Pending | 输出结构化 keyword/cost/target/trigger/effect fields。 |
| P3.4 模板执行器骨架 | Pending | 只做 registry、路由和 unimplemented reason，不接管真实结算。 |
| P3.5 测试、文档、验证、提交 | Pending | 保持现有 `1627/1627` 测试不破坏。 |

## Running

所有 .NET 命令先执行：

```bash
source scripts/dev-env.sh
```

计划验证：

```bash
source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"
git diff --check
```

## Latest Validation

待 P3 实现完成后更新。

## Next Step

实现 P3.1-P3.4 的只读行为规格层、解析管线和模板执行器骨架，然后补齐测试与验证记录。
