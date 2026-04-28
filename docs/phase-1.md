# 第一阶段开发计划

## 阶段目标

建立 .NET 服务端的最小生产化骨架：协议、SignalR 入口、房间串行、幂等、事件日志、玩家视角快照和基于五份 PDF/FAQ 的 conformance tests。旧 Java 输出只作为 legacy oracle 对照。

## 当前进展

- 本机已安装 .NET 10.0.203、PostgreSQL 16、Redis、Node 24。
- `Riftbound.slnx` 已建立，solution 级 build/test 可用。
- `Riftbound.Persistence` 已接入 `IMatchJournal` 和 PostgreSQL P1 schema；journal 行已写入 `ruleset_version`、`faq_version`、`rules_audit_status`、`rules_evidence`。
- `Riftbound.CardCatalog` 已能加载官网快照，并生成功能逻辑单元。
- 当前测试覆盖：
  - 重复 `clientIntentId` 不推进 tick。
  - 协议 envelope 核心字段稳定。
  - 官网快照 1009 条加载。
  - 功能逻辑单元 811 个基线一致。
  - fixture runner 可读取 command log 格式并回放到 `MatchSession`。
  - 已从旧 Java 导出首批 3 条 legacy fixture：`java-oracle-p1-pass`、`java-oracle-p1-end-turn`、`java-oracle-p1-duplicate-pass`。
  - C# 测试可读取 Java fixture 元数据。
  - `PASS`、`END_TURN`、重复 `PASS` 的事件日志 fixture 已与 C# 当前规则骨架对齐；已完成首轮 FAQ 冲突检查，但最终 expected 仍需按协议语义重审。
  - `ConformanceFixture` 已能读取可选 `rulesEvidence`、`faqVersion`、`auditStatus`、`seed`；旧 Java fixture 仍因 `auditStatus = NEEDS_RULE_AUDIT` 被视为 `RequiresRuleAudit`。
  - `docs/rules-evidence-index.md` 已建立五份 PDF/FAQ 到规则域和当前 fixture 的索引。
  - Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。
  - P1 SQL 已补 ruleset/FAQ/audit 字段和 `oracle_fixtures` 索引表；启动时按 `Sql/*.sql` 顺序初始化/迁移。
  - 当前 3 条 Java legacy fixture 的 evidence 已细化；FAQ 未发现推翻通用 `PASS` / `END_TURN` 的条目，但 `PASS -> TURN_ENDED` 被标记为 legacy mismatch candidate。
  - 协议层已新增 `PASS_PRIORITY`、`PASS_FOCUS`，并记录 `END_TURN` 与 legacy `PASS` 的语义边界。

下一步扩展 P1/P2 加入、座位状态和玩家视角 snapshot；新增 fixture 不再使用裸 `PASS`。

## 不做范围

- 不迁移全部卡牌。
- 不重做最终产品级前端 UI；允许在联机底座可用后做简洁精美的开发期测试 UI。
- 不实现复杂 AI。
- 不做移动端适配。
- 不做多实例房间热迁移。

## 验收清单

- `GameHub` 可让 P1/P2 加入同一房间。
- `MatchSession` 对同一房间命令严格串行。
- `clientIntentId` 重复提交不推进 tick，不重复写事件。
- 协议层明确区分 `PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN`，裸 `PASS` 仅用于 legacy 兼容。
- `IRuleEngine` 的输出能被事件和快照投影消费。
- PostgreSQL EventStore schema 草案完成，包含规则版本/FAQ/audit 元数据，并能由开发环境启动时初始化。
- 官网卡牌快照可在新项目中加载，1009 官方条目和 811 功能逻辑单元基线测试通过。
- solution 级 `restore/build/test` 可作为新窗口默认验证入口。
- PDF/FAQ 规则依据覆盖首批高价值路径。
- Java legacy oracle fixture 至少覆盖 10 条高价值路径，或已记录与 PDF/FAQ 的差异。
- C# conformance tests 能读取 fixture 并输出稳定 diff。
- 一旦开发期测试 UI 接入，阶段验收必须用 Codex 内置浏览器执行真实 P1/P2 smoke，并记录 roomId、操作路径、事件和最终 snapshot 摘要。
