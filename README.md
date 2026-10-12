# 符文战场双人对战平台

Godot .NET 原生 macOS / Windows 客户端与 .NET 服务端。玩家提交意图，服务端裁定合法性、费用、结算和计分；React DevUi 用于调试与协议验证。

当前为 **NOT READY**。开发先读 [当前状态](docs/PROJECT_STATUS.md)、[项目治理](docs/PROJECT_GOVERNANCE.md) 和 [测试工作流](docs/RULE_TEST_WORKFLOW.md)。历史 CURRENT/P 阶段记录不再充当任务调度或规则认证。

2026-10-12 续跑实取 CN 1,502、EN 1,381 个印次，16 份中文 PDF 和 20 份全球来源；英文报告总数仍比返回数多 8 项。当前并集含 1,738 个卡牌身份和 14,293 条规则/示例/QA，逐项语义认证仍未完成。来源快照不自动表示官网此刻未变，见 [当前版本入口](docs/PROJECT_STATUS.md)。

代码基线 442114f0 的 [完整 CI](https://github.com/DDDHL0704/riftbound/actions/runs/38205666224) 已通过 10,201 用例、6,625 方法及 110 个双向牌组配对，与上一基线的全部身份和配对最终哈希相同。macOS/Windows 构建与网络恢复成功；可见双客户端、Windows 实机、真实 PostgreSQL 和全量官方语义仍有独立验收缺口。该结果仅属于指定基线，后续改动须记录自己的验证范围。

## 本地运行

环境需要 .NET 10 SDK、Godot .NET 4.7.2（客户端目标 net8.0），导出需要同版本模板。本机路径可由 scripts/dev-env.sh 加入 PATH，其他机器按实际安装配置。

启动内存后端：

```sh
source scripts/dev-env.sh
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5088 \
ConnectionStrings__Riftbound="" \
dotnet run --project src/Riftbound.Api
```

另一个终端启动客户端：

```sh
source scripts/dev-env.sh
dotnet build clients/godot/Riftbound.GodotClient.csproj -m:1
"$RIFTBOUND_GODOT_BIN" --path clients/godot -- \
  --riftbound-server=http://127.0.0.1:5088
```

双客户端使用不同身份。操作、连接与导出说明见 [Godot README](clients/godot/README.md)，Windows 分发须包含 ZIP 中 EXE 与完整相邻运行时目录。

## 开发与验证

```sh
python3 scripts/test-rules.py focus combat
python3 scripts/test-rules.py check
python3 scripts/test-rules.py full --plan-only
python3 -m unittest discover -s scripts/tests -v
python3 scripts/audit/check-project-governance.py
```

规则开发用 focus 和相邻域，连贯批次一次 check；完整 CI 在固定提交上后台运行。最终候选仍须完整回归与相应原生验收。明确治理文档白名单可走轻量 CI；数据、fixture、脚本、构建配置及未知范围默认完整验证。轻量通过不表示执行了规则或打包测试。

## 项目结构

| 路径 | 职责 |
|---|---|
| clients/godot | 原生桌面、命令适配与打包 |
| src/Riftbound.Contracts | 命令、事件、快照和提示 |
| src/Riftbound.CardCatalog | 官方卡池与解析元数据 |
| src/Riftbound.Engine | 共享裁定、结算、投影、回放与恢复 |
| src/Riftbound.Persistence | PostgreSQL journal 与恢复 |
| src/Riftbound.Api | HTTP/SignalR、身份、房间和匹配 |
| src/Riftbound.DevUi | 调试与协议验证 |
| tests/Riftbound.* / scripts/tests | 行为证据与验证工具检查 |
| data/official / docs/evidence | 有版本的来源快照和限定证据 |

任务与规格统一使用 [GitHub Issues](https://github.com/DDDHL0704/riftbound/issues)，词汇见 [GLOSSARY](GLOSSARY.md)。9 月及更早规则默认未重新认证，按当前适用官方来源逐项整改。后续页面使用 frontend-design 与 Product Design，并重新取得参考截图。用户已确认创建新Goal，持续审计至全量证据闭合或明确暂停。
