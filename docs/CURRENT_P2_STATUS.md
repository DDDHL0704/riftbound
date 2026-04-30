# Current P2 Status

更新时间：2026-04-30

这是新窗口优先读取的短交接文件。它只记录当前开发状态和下一步，完整规则证据仍以 `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 和各 fixture 的 `rulesEvidence` 为准。

## Snapshot

- 当前 P2 功能基线：已覆盖 `OGN·212/298 未来熔炉` 的当前 preflight 装备入场到基地并打出一名 1 战力“随从”路径；最新提交以 `git log -1 --oneline` 为准
- 上一个 P2 功能基线：`SFD·192/221 舒瑞娅的安魂曲` 的当前 preflight 装备入场到基地并让所有友方单位变为活跃路径
- 最近全量验证：`dotnet test Riftbound.slnx --no-restore` 通过 `362/362`
- 最小 card behavior registry：`177/811 = 21.8%`
- P2 preflight 清单：已完成到 `210`，下一项是 `211. 逐批迁移更多低复杂度官方卡牌`
- 当前工作区预期：只剩未跟踪的 `riftbound-dotnet.sln`，不要提交它，除非用户明确要求

## Current Focus

继续 P2 core rules preflight，逐批迁移低复杂度官方卡牌/模式。优先选择：

- 费用清晰、单目标或少量目标
- 伤害、摧毁、回手、抽牌、本回合效果
- 简单标签、装备对象、战力修正
- 能复用现有 `CardBehaviorRegistry`、`CoreRuleEngine` 和 fixture runner 能力的路径

暂不进入：

- 最终产品 UI
- 一次性全卡牌迁移
- 完整战斗/得分大系统
- 复杂 AI
- 移动端适配
- 提交规则 PDF/FAQ

## Acceleration Mode

后续 P2 preflight 默认采用“同能力族小批次”节奏，在不降低审计要求的前提下减少重复开销：

- 每批优先选择 `3-5` 张可复用同一 engine 原语的低复杂度卡牌/模式；如果需要新增小原语，批次可缩小到 `1-2` 张。
- 每张卡仍必须补齐 registry/card behavior、`rulesEvidence`、fixture、conformance 测试、`docs/rules-evidence-index.md` 和 `docs/p2-rules-preflight.md`。
- 批内每张新增后先跑目标过滤测试；批末统一跑 `FullyQualifiedName~ConformanceFixtureRunnerTests`、全量 `dotnet test Riftbound.slnx --no-restore`、`git diff --check`，再提交。
- 优先连续推进同一能力族：0 目标抽牌/召符文/创建指示物、单目标伤害、单目标回手/移动、本回合战力修正/标签添加、简单装备对象。
- 优先补“解锁型小原语”，例如废牌堆回收 N 张、装备自毁激活、最多 N 个目标、按标签/类型计数、单位/装备 token 参数化。
- 每次停下来仍要提交已完成改动，并汇报实时 `x/811` 整体和当前 Part 百分比；不要提交未跟踪的 `riftbound-dotnet.sln`。

## Latest Completed

- `SFD·077/221 火箭轰击`：新增 `DESTROY_EQUIPMENT` 模式，覆盖装备目标摧毁和单位目标拒绝。
- `SFD·135/221 紧急召回`：装备返回拥有者手牌，补 `EQUIPMENT_RETURNED_TO_HAND` 事件和单位目标拒绝。
- `OGN·022/298 热电光束`：摧毁所有场上装备，非装备单位不受影响。
- `SFD·162/221 血钱`：摧毁战场上不高于 2 战力单位后，按敌/友方分支打出 1/2 枚休眠“金币”装备指示物。
- `SFD·147/221 坠渊之流`：让所有当前场上单位和装备返回所属者手牌，并移除公开对象状态。
- `OGN·179/298 折戟再战`：当前 2P preflight 中按目标顺序记录双方各自选择一件自己的装备，双方让过后分别摧毁。
- `OGN·224/298 废物利用`：补齐选择一件装备时先摧毁该装备、再抽 1 张牌的分支；不选择装备分支仍覆盖。
- `SFD·005/221 印爆术`：摧毁装备后让目标控制者抽 2 张牌。
- `UNL-070/219 化为灰烬`：装备获得 `瞬息` 标签。
- `SFD·070/221 痛苦之酬`：战场单位 3 点伤害后打出休眠“金币”装备指示物。
- `OGN·069/298 背水一战`：友方单位本回合按当前战力翻倍并获得 `瞬息`。
- `OGN·180/298 逝水如镜`：战场单位或装备获得 `瞬息`。
- `OGN·094/298 精灵召唤` / `UNL-069/219 精灵迸发`：生成的 3 战力“精灵”单位指示物记录 `瞬息` 标签。
- `UNL-200/219 镜花水月`：在当前对象模型中打出活跃“映像”到基地，复制目标当前战力和标签并获得 `瞬息`。
- `OGN·008/298 罪恶快感`：弃置一张友方手牌后，按被弃牌 `manaCost` 对战场单位造成非致命伤害。
- `OGN·071/298 次元门狂欢`：当前 2P preflight 用 mode 记录对手选择“卡牌”或“符文”，分别覆盖双方各抽 1 或双方各召出 1 枚休眠符文。
- `SFD·004/221 丛林伏击`：当前 preflight 覆盖打出一枚休眠“金币”装备指示物；友方单位本回合活跃进场的全局效果暂缓。
- `SFD·198/221 沙兵现身`：当前 preflight 覆盖控制 0 件武装时不创建新单位、让两名既有“黄沙士兵”变为活跃状态；按武装数量创建黄沙士兵暂缓。
- `SFD·166/221 集结部队`：当前 preflight 覆盖结算后的即时抽 1；友方单位进场给予增益的全局触发暂缓。
- `OGN·129/298 迎敌号令`：当前 preflight 覆盖结算后的即时抽 1；本回合单位活跃进场的全局效果暂缓。
- `OGN·053/298 秘奥义！慈悲度魂落`：当前 preflight 覆盖给予一名友方单位 `增益` 标签并永久 +1 战力；本回合所有增益额外 +1 的全局效果暂缓。
- `OGN·270/298 叹为观止`：当前 preflight 覆盖给予基地友方单位 `增益` 标签、永久 +1 战力，并移动到当前单战场区域。
- `SFD·188/221 虚空猛冲`：当前 preflight 覆盖不选择免费打出顶部牌时抽取两张展示牌；免费打出分支暂缓。
- `OGN·153/298 公开行动`：当前 preflight 覆盖无既有增益可消耗时给予所有友方单位 `增益` 标签并永久 +1 战力。
- `UNL-083/219 镜中幻影`：当前 preflight 覆盖两名不同公开区域友方单位中至少一名拥有 `瞬息` 时互换位置并抽 1；精确多战场位置和待命/反应时机暂缓。
- `OGN·250/298 天声震落`：当前 preflight 覆盖基地友方单位按自身战力伤害敌方战场单位，然后移动到当前单战场区域。
- `UNL-101/219 战斗号令`：当前 2P preflight 用目标顺序记录友方单位和对手所选单位，双方让过后将两名基地单位移动到当前粗粒度战场区域。
- `OGN·268/298 弹幕时间`：当前 preflight 用 `optionalCosts = ["SPEND_POWER:n"]` 记录符能支付，并按支付数值伤害敌方战场单位。
- `OGN·102/298 传送门大营救`：当前 preflight 覆盖友方战场单位被放逐后重新打出到所属基地，并清除场上伤害、本回合内战力修正与本回合内效果。
- `UNL-184/219 狩猎律动`：当前 preflight 覆盖友方单位被放逐后重新打出到当前粗粒度战场，并清除场上伤害、本回合内战力修正与本回合内效果。
- `OGN·198/298 蚀魂夜`：当前 preflight 覆盖己方废牌堆单位牌被打出到基地，并拒绝非单位废牌堆目标；完整目的地选择暂缓。
- `UNL-202/219 虚空来袭`：当前 preflight 覆盖友方单位和敌方单位按目标顺序移动到当前粗粒度战场；战场控制/进攻方细节暂缓。
- `OGN·258/298 猛龙摆尾`：当前 preflight 覆盖敌方单位移动到另一名敌方单位所在位置后，两者以自身战力互伤；友方/重复目标拒绝覆盖，多战场精确目的地暂缓。
- `SFD·184/221 冷酷追击`：当前 preflight 覆盖友方战场单位移动到所属基地，并获得本回合征服后可召回标记；敌方/非单位目标拒绝覆盖，可选贴附武装和征服后触发结算暂缓。
- `SFD·046/221 魄罗佳肴`：当前 preflight 覆盖装备从手牌打出后成为基地装备对象并抽 1；带目标打出拒绝覆盖，自毁激活技能暂缓。
- `SFD·192/221 舒瑞娅的安魂曲`：当前 preflight 覆盖专属装备从手牌打出后成为基地装备对象，并让控制者所有单位变为活跃状态；带目标打出拒绝覆盖，唯我和装配技能暂缓。
- `OGN·212/298 未来熔炉`：当前 preflight 覆盖装备从手牌打出后成为基地装备对象，并打出一名 1 战力“随从”单位到控制者基地；带目标打出拒绝覆盖，摧毁装备回收废牌堆分支暂缓。
- `UNL-165/219 暗影的召唤`：友方单位获得 `瞬息` 后抽 2 张牌。
- `OGN·264/298 游击战`：最多两张己方废牌堆待命牌返回手牌。

## Required Per Card

每个新增能力仍必须补齐：

- registry/card behavior 参数
- fixture，且包含 `rulesEvidence`
- conformance 测试
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md` 的 fixture/进度项

长摘要文档只做短状态维护；不要再把完整已覆盖卡牌列表重复粘到 `START_HERE.md`、`phase-1.md`、`development-audit-status.md` 或 `master-development-plan.md`。

## New Window Read Order

默认只读：

1. `docs/CURRENT_P2_STATUS.md`
2. `README.md`
3. `docs/p2-rules-preflight.md` 的最近进度和相关 fixture 段落
4. `docs/rules-evidence-index.md` 中目标卡牌对应行
5. `docs/conformance-fixture-format.md` 中 fixture schema 规则

按需再读：

- `docs/START_HERE.md`：项目边界和总体目标
- `docs/master-development-plan.md`：阶段计划
- `docs/phase-1.md`：P1/P2 过渡状态
- `docs/protocol-semantics.md`：命令和事件语义
- `docs/rules-card-baseline.md`：卡牌基线和官方目录说明
- `docs/rules-authority-and-audit.md`：规则权威和审计原则
- `docs/development-audit-status.md`：模块级审计状态
