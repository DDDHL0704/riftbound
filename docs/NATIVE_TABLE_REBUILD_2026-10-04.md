# 原生对战牌桌重构与验收 · 2026-10-04

本轮同时处理“操作太绕”和“局势不清楚”。交付仍是 Godot .NET 原生客户端；合法选择、费用、移动、争夺、优先权、伤害与区域迁移均由后端裁定。

## 参考与现场审查

- [Rift Atlas](https://riftatlas.com/)：本轮查看其公开模拟器截图，参考中央战场、双方镜像区域、侧边卡牌预览和结算链的空间组织。没有使用其规则作为裁决依据，也没有把产品改为网页版。页面截图保存在 `var/qa/table-redesign/reference-atlas.png`。
- [EggsLeggs / riftbound-tcg-tts](https://github.com/EggsLeggs/riftbound-tcg-tts)：读取项目 README，参考明确的基地、符文、战场、废牌/放逐区域，以及 ready/channel 和计分工作流；没有复制该仓库的脚本或图形素材。
- 当前客户端的重构前截图为 `var/qa/table-redesign/before-table.png`。现场观察：双方公开区过高，战场偏薄；分数与资源被长文字埋没；移动另开全屏选择窗；主要对战页没有持续可见的结算链和近期事件。

参考项目用于交互和视觉布局。规则权威仍为仓库中国区官方资料及 [规则权威说明](rules-authority-and-audit.md)。

## 已实现

1. 中央双战场采用官方战场画面的低透明度背景。控制方、争夺、已得分均读取服务器快照，用文字与颜色共同表示。比分、回合和胜利分数固定在顶部；战力合计仅作展示，不预测伤害或胜负。
2. 双方基地与符文收紧为横向区域，手牌居中、可滚动。固定底部操作栏，牌桌与侧栏分别滚动，避免长卡文或小窗口挤掉确认按钮。
3. 点击手牌直接进入侧栏出牌；在桌面点击服务器允许的单位选择目标，点击战场选择入场位置。原有模式、额外费用、符文资源与权威费用预览保留，目标变更立即使旧预览失效。
4. 点击可移动单位直接选中它；桌面多选单位并点击整块合法战场作为落点。目的地改变时取消不能到达的单位并明确提示。只提交服务器提供的对象与位置，后端仍重新核验。
5. 多个行动共用一张牌时显示行动选择；同一个动作存在多个对象别名/能力时，需要显式选择，不能静默使用第一项。
6. 移动在服务端确认前保持面板打开，阻止重复提交；拒绝保留选择及原因。出牌和移动提交期间不能取消或从底栏发起另一个行动。快照/prompt 改变时旧选择关闭。
7. 悬停查看侧栏详情，右键打开完整官方卡牌，Esc 返回。结算链按下一项优先显示卡名、控制方和目标；事件栏显示服务器实际事件，合并同批重复描述，并把结算对象编号替换为可公开的卡名。隐藏引用不查询或展示牌名。
8. 打包 Noto Sans CJK SC 开源字体，修复新汉字缺字并统一桌面文字回退。来源与 OFL 许可见 `clients/godot/assets/ASSETS.md`。

结构上将布局拆入 `MatchTableLayout`，事件展示拆入 `BattleEventPresenter`，出牌侧栏适配拆入 `PlayCardOverlay.Table`。这些组件不依赖规则引擎。

## 顺带修复的后端场景问题

- 重载开发场景曾复制上一局的 `MatchState`，遗留对象位置、战场记录与任务，使双方停在 WAIT。现在仅保留房间、座位和 tick，从干净状态构造场景。四个“争夺后重载”的测试先失败，修复后与全新场景的状态哈希一致。
- 旧展示场景将单位 `SFD·001/221` 标为符文，经过真实回合清空资源后无法支付印刷符能。展示区与符文牌堆改用官方红色炽烈符文 `OGN·007/298`。依据 `data/official/card-catalog.zh-CN.json` 的 `cardCategory=rune`、`cardColorList=[red]`。新增“经过两个真实回合、横置、回收、支付海克斯射线”的回归，先失败后通过。
- 以上调整限于开发场景构造，不更改正式对战规则或牌组合法性判断。旧的孤立引擎场景未必包含可视战场，不能全部当成原生可玩示例。本轮完整交互从 `standard-group-movement` 场景经真实命令推进得到。

## 验收证据

| 流程 / 门禁 | 结果 |
|---|---|
| 源码构建 | Godot .NET 构建 0 警告、0 错误 |
| 新原生行为门禁 | 1280×720、1440×900、1920×1080 三种配置均输出 `BATTLE_TABLE_INTERACTION_PASS` |
| 门禁覆盖 | 桌面目标、非法对象拒绝、多选移动、重复提交、旧回执、拒绝后保留选择、旧费用、选择过期、多能力歧义、隐藏卡牌、事件隐藏引用及底部按钮边界 |
| 原有原生结构门禁 | 卡牌组件、对战场景、提示控制器、专用覆盖层、响应式结构五项通过；其旧截图检查不替代本轮实机截图 |
| 费用预览回归 | `PLAY_COST_PREVIEW_FRESHNESS_PASS`，保留前轮只读权威预览约束 |
| 后端针对性回归 | 49/49，0 失败，0 跳过，`table-redesign-focused-final.trx` |
| 后端全量回归 | 9344/9344，0 失败，0 跳过，5 分 10 秒，`table-redesign-full-final.trx` |
| macOS 多选移动 | 两个单位由桌面选中后同时移至敌方战场，后端记录两次移动和一次争夺，休眠与战力即时更新 |
| macOS 实付与结算 | 真实推进双方回合后，点击敌方单位，回收炽烈符文，支付 1 法力＋1 红色符能；双方让过后造成 3 点伤害，目标进入废牌堆，结算链清空 |
| macOS 可视验收 | 1440 下移动；1280 下完成桌面选目标、资源支付、双方响应；1920 下最终包运行、右键详情、Esc 返回和费用确认。主行动区域完整可见，溢出内容可滚动 |
| Windows | x64 可执行文件与托管运行库已导出打包；没有 Windows 实机验收 |

复验命令：

```sh
dotnet build clients/godot/Riftbound.GodotClient.csproj
RIFTBOUND_GODOT_BIN=/path/to/Godot bash clients/godot/tools/check-battle-table-interaction.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter 'FullyQualifiedName~DevScenarioResetTests'
```

机器可读记录、包校验值与关键截图索引见 [验收清单](evidence/native-table-rebuild-acceptance.json)。关键截图与服务器事件保存在 `docs/evidence/native-table-rebuild/`；完整本地过程证据在 `var/qa/table-redesign/`；测试 TRX 在 `tests/Riftbound.ConformanceTests/TestResults/`。macOS 最终包已通过 `codesign --verify --deep --strict` 检查，未进行公证发行验收。

## 交付边界

本轮完成的是原生对战页和相关状态问题，不宣告全卡牌、全部规则或正式发行就绪。前轮尚未关闭的常驻牌确认时机、效果引发再次打出，以及 Windows 实机验收仍按 [后续计划](NEXT_NATIVE_PLAY_ITERATION_2026-10-04.md) 保留。此次不发布网站，也未推送 GitHub 或运行远端 CI。
