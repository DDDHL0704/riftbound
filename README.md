# 符文战场双人对战平台

Godot .NET 原生 macOS / Windows 客户端与 .NET 服务端。玩家提交意图，服务端裁定合法性、费用、结算和计分；React DevUi 用于调试与协议验证。

当前处于服务端规则开发阶段：Codex 按 [待办清单](docs/BACKLOG.md) 开发规则与卡牌，网页对战前端在 [clients/web](clients/web/README.md)。开发流程、测试命令与底线见 [AGENTS.md](AGENTS.md)。

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
dotnet test tests/Riftbound.ConformanceTests --filter "FullyQualifiedName~<类名或方法名>"
python3 scripts/test-rules.py focus combat
python3 -m unittest discover -s scripts/tests
```

PR 由 `Backend L2` 检查（全部规则测试分片 + 两局完整对局），通过即可合入；每晚 02:00（北京时间）对 main 跑完整验收。详见 [AGENTS.md](AGENTS.md)。

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
| clients/web | 网页对战前端 |
| data/official | 官方来源快照 |
| docs/BACKLOG.md | 待办与状态 |

词汇见 [GLOSSARY](GLOSSARY.md)。重置前的历史文档在 Git 标签 `archive/pre-reset-20261029` 中。
