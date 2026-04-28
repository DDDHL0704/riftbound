# 第一阶段开发计划

## 阶段目标

建立 .NET 服务端的最小生产化骨架：协议、SignalR 入口、房间串行、幂等、事件日志、玩家视角快照和 Java oracle conformance tests。

## 当前进展

- 本机已安装 .NET 10.0.203、PostgreSQL 16、Redis、Node 24。
- `Riftbound.slnx` 已建立，solution 级 build/test 可用。
- `Riftbound.Persistence` 已接入 `IMatchJournal` 和 PostgreSQL P1 schema。
- `Riftbound.CardCatalog` 已能加载官网快照，并生成功能逻辑单元。
- 当前测试覆盖：
  - 重复 `clientIntentId` 不推进 tick。
  - 协议 envelope 核心字段稳定。
  - 官网快照 1009 条加载。
  - 功能逻辑单元 811 个基线一致。
  - fixture runner 可读取 command log 格式并回放到 `MatchSession`。

下一步从 Java oracle fixture exporter 开始，并把 exporter 输出接入当前 C# fixture runner。

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
- `IRuleEngine` 的输出能被事件和快照投影消费。
- PostgreSQL EventStore schema 草案完成，并能由开发环境启动时初始化。
- 官网卡牌快照可在新项目中加载，1009 官方条目和 811 功能逻辑单元基线测试通过。
- solution 级 `restore/build/test` 可作为新窗口默认验证入口。
- Java oracle fixture 至少覆盖 10 条高价值路径。
- C# conformance tests 能读取 fixture 并输出稳定 diff。
- 一旦开发期测试 UI 接入，阶段验收必须用 Codex 内置浏览器执行真实 P1/P2 smoke，并记录 roomId、操作路径、事件和最终 snapshot 摘要。
