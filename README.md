# 符文战场双人对战平台

Godot .NET 原生桌面客户端（macOS / Windows）与 .NET 权威服务端。玩家提交操作意图，后端判断合法性、支付费用、结算效果和计分；客户端只显示服务端提供的局面、合法候选和结果。

## 当前状态

正式双人对战采用中国区官方规则与禁限牌。当前仍为开发版本，**NOT READY**，尚未达到全部官方规则、全部卡牌和双平台实机验收完成的标准。

- 原生大厅、预组选择、匹配/房间、换牌、牌桌、卡牌详情、公共牌堆浏览、群组移动、出牌费用选择、整方战斗伤害分配及网络恢复已接入后端。
- macOS universal 与 Windows x86_64 完整包可以导出；macOS 已从源码目录外启动并验证若干实连操作，Windows 仍缺少实机运行证据。macOS 使用本地 ad-hoc 签名，尚未发行签名、公证或部署公网后端。
- 2026-10-04 官方资料快照包含 1,374 条卡牌和 13 份规则、FAQ、勘误及禁卡文档。运行卡池仍为原 1,009 条；新增卡牌需完成共享规则能力和验证后才能开放。
- 最近保存的后端一致性回归为 2026-10-04 的 9,344/9,344，零失败、零跳过，见 [牌桌重构验收](docs/evidence/native-table-rebuild-acceptance.json)。这证明现有测试通过，不能替代对全部官方条款和卡牌的逐项审核。
- React DevUi 用于调试与协议回归，当前产品交付目标是原生客户端。

最新牌桌变更、截图与验收见 [原生牌桌重构记录](docs/NATIVE_TABLE_REBUILD_2026-10-04.md)，此前平台交付见 [原生交付记录](docs/NATIVE_DELIVERY_2026-10-04.md)。GitHub 原生 CI 已编写，尚缺运行证据；此前记录的 workflow 推送权限问题在本轮规划中未重新验证。

下一批按成品牌桌样式与直接操作、普通出牌确认与反馈、效果再次打出、完整对局及双平台验收四步推进，任务、参考截图和完成标准见 [2026-10-05 迭代计划](docs/NEXT_NATIVE_PLAY_ITERATION_2026-10-05.md)。共享费用与权威预览已完成的部分沿用 [实施记录](docs/UNIFIED_PLAY_ITERATION_2026-10-04.md)。

## 本地运行原生客户端

环境需要 .NET 10 SDK、Godot .NET 4.7.2（客户端目标 net8.0）。导出还需要同版本 Godot 模板。本机已有工具可由 `scripts/dev-env.sh` 加入 PATH；其他机器按自身安装路径配置。

启动本地内存后端：

```sh
source scripts/dev-env.sh
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5088 \
ConnectionStrings__Riftbound="" \
dotnet run --project src/Riftbound.Api
```

在另一个终端启动原生客户端，将 `RIFTBOUND_GODOT_BIN` 设为 Godot .NET 可执行文件的实际路径：

```sh
source scripts/dev-env.sh
dotnet build clients/godot/Riftbound.GodotClient.csproj
"$RIFTBOUND_GODOT_BIN" --path clients/godot -- \
  --riftbound-server=http://127.0.0.1:5088
```

启动两个客户端并使用不同身份，可在同一房间对战。客户端操作、隔离身份及实连测试说明见 [Godot README](clients/godot/README.md)。

## 构建和验证

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests
clients/godot/tools/export-desktop.sh macOS
clients/godot/tools/export-desktop.sh windows
```

导出脚本使用 `RIFTBOUND_GODOT_BIN`，产物在 `clients/godot/exports/`。Windows 必须分发 ZIP 内的 EXE 及相邻完整运行时目录。构建成功不代表跨平台交互或全部规则已验收。

## 项目结构与规则证据

| 路径 | 职责 |
|---|---|
| `clients/godot` | 原生桌面客户端、导出与原生测试工具 |
| `src/Riftbound.Contracts` | 命令、事件、快照及提示协议 |
| `src/Riftbound.Engine` | 权威规则、串行命令、区域、结算链与回放状态 |
| `src/Riftbound.Api` | ASP.NET Core + SignalR、身份、房间和匹配 |
| `src/Riftbound.Persistence` | PostgreSQL 日志持久化 |
| `src/Riftbound.CardCatalog` | 官方资料加载、规则文本与 BehaviorSpec |
| `src/Riftbound.DevUi` | 调试界面、协议和实连回归工具 |
| `tests/Riftbound.ConformanceTests` | 官方规则依据、命令结算、投影与回放测试 |
| `data/official/upstream/2026-10-04` | 最新中国区来源清单、SHA-256、卡牌差异和条款审核清单 |
| `docs` | 规则证据、当前实施记录及历史阶段记录 |

继续开发时先读 [原生交付记录](docs/NATIVE_DELIVERY_2026-10-04.md)、[规则权威](docs/rules-authority-and-audit.md)、[证据索引](docs/rules-evidence-index.md) 和 [服务端审核](docs/CURRENT_SERVER_RULE_AUDIT.md)。规则以中国区官网卡面和正式 PDF 为准；旧 Java 行为与历史 fixture 只能作为对照。通用机制必须在共享引擎中实现，并验证隐藏信息、持久化/回放和拒绝操作不变性。

`CURRENT_P7_STATUS.md`、`CURRENT_P7_9_STATUS.md` 和早期 Web 重建计划保留历史记录，不能作为当前原生产品或全规则完成证明。测试 fixture 格式见 [格式说明](docs/conformance-fixture-format.md)。
