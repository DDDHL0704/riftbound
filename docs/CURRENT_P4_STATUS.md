# Current P4 Status

更新时间：2026-05-03

这是 P4 高频关键词与基础卡牌的短状态文件。P3 的卡牌数据与 BehaviorSpec 只读骨架完成状态仍以 `docs/CURRENT_P3_STATUS.md` 为准；P2 core rules preflight 与 P2.5 开发期测试 UI 状态分别以 `docs/CURRENT_P2_STATUS.md` 和 `docs/CURRENT_P2_5_STATUS.md` 为准。

## Goal

完成 P4 高频关键词与基础卡牌：按风险分层小批次实现权限关键词、战斗关键词、生命周期关键词、资源关键词、互动关键词、装备关键词和基础动作模板，复用 P3 BehaviorSpec/template skeleton，保持 P2/P2.5/P3 绿色，补测试、文档、状态文件并提交。

## P4.0 Scope

本阶段只做审计与候选分层：

- 读取 P2/P2.5/P3 状态、P4 主计划、README、START_HERE、BehaviorSpec contracts、BehaviorSpec catalog、规则文本 parser、template executor、CardBehaviorRegistry、CoreRuleEngine、catalog baseline tests、conformance runner tests 和官方卡表快照。
- 通过本地 API `/catalog/p3-status` 与 `/catalog/behavior-specs` 只读复核 P3 BehaviorSpec 与 template report。
- 建立 P4 关键词和基础动作模板候选清单、风险分层、推荐小批次顺序和验证门禁。
- 不改 `CoreRuleEngine` 主路径，不启用 P3 template executor 写状态，不迁移全卡牌。

本阶段明确不做：

- 不进入 P5 装备/控制权/触发替换大系统。
- 不进入 P6 全卡牌批量实现。
- 不进入 P7 最终产品 UI。
- 不提交规则 PDF/FAQ。
- 不提交未跟踪的 `riftbound-dotnet.sln`。

## Baseline

- P3 完成提交：`4a3b45f feat: complete p3 card behavior specs`
- P4.0 提交：`fb19570 docs: add p4 status audit`
- P4.1 提交：`506ca89 feat: add p4 template delegation bridge`
- P4.2 提交：`9ed58b0 feat: add p4 permission keyword model`
- P4.3 提交：`64a96fe feat: add p4 ephemeral turn start cleanup`
- P4.4 提交：`4c72486 feat: add p4 echo keyword model`
- P4.5 提交：`209ad75 feat: add p4 primitive template plans`
- P4.6 提交：`0693e27 feat: add p4 combat keyword profiles`
- P4.7 提交：`c93cb87 feat: add p4 resource keyword profiles`
- P4.8 提交：`c376b94 feat: add p4 equipment keyword profiles`
- P4.9 提交：`60396c5 feat: add p4 remaining profile audit`
- P4.10 提交：`af40d9f feat: add p4 fixed experience gain`
- P4.11 提交：`10a2256 feat: add p4 experience optional cost`
- P4.12 提交：`fc6046c feat: add p4 spellshield target tax`
- P4.13 提交：`1f2347d feat: add p4 haste ready optional cost`
- P4.14 提交：`64e26c2 feat: add p4 encourage cost reduction`
- P4.15 提交：`3bc7060 feat: add p4 level threshold source unit`
- P4.16 提交：`df6f5b3 feat: add p4 windrunner level roam`
- P4.17 提交：`c7c8aa7 feat: add p4 wuji level draw`
- P4.18 提交：`36723be feat: add p4 baby shark haste ready`
- P4.19 提交：`55a7c43 feat: add p4 dynamic experience gain`
- P4.20 提交：`38d7d74 feat: add p4 legion rearguard haste ready`
- P4.21 提交：`812c7bc feat: add p4 encourage self boon`
- P4.22 提交：`0d4ada5 feat: add p4 dangerous duo encourage`
- P4.23 提交：`9da6828 feat: add p4 junkyard bully encourage`
- P4.24 提交：`6b4bd28 feat: add p4 vanguard captain encourage`
- P4.25 提交：本提交 `feat: add p4 mr root haste ready`
- 官方快照：`data/official/card-catalog.zh-CN.json`
- 快照日期：`2026-04-27`
- 官方条目：`1009`
- Functional units：`811`
- P2 core rules preflight：`811/811 = 100.0%`
- 最小 card behavior registry：`794/811 = 97.9%`
- 可打出官方牌差集：已清空
- P3 schema validation：`1009/1009`，violations `0`
- P3 BehaviorSpec：`1009/1009`
- P3 BehaviorSpec status counts：`implemented 785`、`manual-rule-required 211`、`unimplemented 13`
- P3 missing reason：`0`
- 工作区预期：只剩未跟踪 `riftbound-dotnet.sln`

## P4.0 Audit Snapshot

复核命令：

```bash
source scripts/dev-env.sh && ASPNETCORE_URLS=http://127.0.0.1:5091 dotnet run --no-restore --project src/Riftbound.Api/Riftbound.Api.csproj
curl -s http://127.0.0.1:5091/catalog/p3-status
curl -s http://127.0.0.1:5091/catalog/behavior-specs
```

`/catalog/p3-status` 结果：

```json
{
  "officialEntries": 1009,
  "total": 1009,
  "schemaValid": true,
  "schemaViolationCount": 0,
  "functionalUnits": 811,
  "idsAreUnique": true,
  "behaviorSpecs": 1009,
  "statusCounts": {
    "implemented": 785,
    "manual-rule-required": 211,
    "unimplemented": 13
  },
  "missingReasonCardNos": []
}
```

解释：

- `implemented` 仍表示 P3 spec 能映射到现有 P2 手写 registry 或同 functional unit 映射，不表示 P4 keyword/template 已规则化执行。
- `manual-rule-required` 当前主要是 `传奇 106`、`战场 57`、`符文 48`，需要独立规则域。
- `unimplemented 13` 均为指示物/指示物战场/指示物装备，需要 token factory 或非 `PLAY_CARD` 绑定。
- `BehaviorTemplateExecutor` 当前只生成 plan，不改 `MatchState`；P4.1 只能先做安全桥接测试，不能替换 `CoreRuleEngine`。

## Template Candidates

以下统计按 P3 `BehaviorSpec.TemplateIds` 的 distinct card count 计算。

| Template | Total | Implemented | Manual | Unimplemented | P4 risk | P4.0 decision |
|---|---:|---:|---:|---:|---|---|
| `temp_might` | 292 | 255 | 36 | 1 | Low/Medium | P4.5 已有 primitive plan；真实状态写入仍由 P2 `POWER_MODIFIED_UNTIL_END_OF_TURN` 与清理负责。 |
| `damage` | 148 | 141 | 7 | 0 | Low/Medium | P4.5 已有基础固定伤害 primitive plan；动态伤害和替代效果继续委托 P2。 |
| `move` | 136 | 116 | 19 | 1 | Medium | 当前只桥接/委托 P2；精确多战场/游走/此处目的地仍需后续模型。 |
| `draw` | 131 | 105 | 26 | 0 | Low | P4.5 已有固定抽牌 primitive plan；抽牌与燃尽状态写入仍由 P2 覆盖。 |
| `destroy` | 127 | 115 | 8 | 4 | Low/Medium | P4.5 已有单目标摧毁 primitive plan；替代/触发导致的摧毁仍分层处理。 |
| `assemble` | 55 | 53 | 2 | 0 | High | 暂不进 P4.1；涉及贴附、owner/controller、费用与 P5 边界。 |
| `gain_experience` | 51 | 43 | 8 | 0 | Medium/High | P4.10 已接入固定数值“打出时获得经验”；P4.11 已接入固定经验额外费用减费代表路径；P4.19 已接入《严厉军士》按友方场上单位数量获得经验代表路径；经验激活技能和等级/装配联动仍 deferred。 |
| `recall` | 49 | 39 | 10 | 0 | Medium | 当前只桥接/委托 P2；召回到基地/手牌已有 P2 原语，精确时序分层。 |
| `stun` | 33 | 30 | 3 | 0 | Low | P4.5 已有 `STUNNED` primitive plan；P3 parser 的眩晕 reminder damage 噪声不会阻断该 primitive。 |
| `echo` | 24 | 22 | 2 | 0 | Medium | P4.4 已将 mana-only `ECHO` optional cost/repeat 抽成互动关键词模型；有色/弃牌/授予回响仍延后。 |
| `ambush` | 18 | 18 | 0 | 0 | High | P4.9 已识别 profile；待命/反应/战场目的地和 face-down 交互仍 deferred。 |

## Keyword Candidates

以下统计按 P3 `BehaviorSpec.Keywords` 的 distinct card count 计算。

| Keyword | Implemented | Manual | Unimplemented | P4 risk | P4.0 decision |
|---|---:|---:|---:|---|---|
| 迅捷 | 82 | 0 | 0 | Medium | P4.2 候选；需把普通回合/法术对决时机从卡牌特例提升为关键词模型。 |
| 反应 | 136 | 14 | 2 | Medium/High | P4.2 候选；P2 已有 `CanPlayDuringPriority`，但符文/装备/指示物反应需分域。 |
| 急速 | 34 | 0 | 0 | Medium | P4.2 已识别 profile；P4.13 已给《灼焰飞龙》接入 `HASTE_READY` 代表可选费用，P4.18 已给《小鲨鱼》接入第二条 `HASTE_READY` 代表可选费用，P4.20 已给《军团后卫》接入第三条 `HASTE_READY` 代表可选费用，P4.25 已给《树根先生》接入第四条 `HASTE_READY` 代表可选费用，其他急速额外费用仍 deferred。 |
| 强攻 | 37 | 2 | 0 | High | P4.6 已识别 profile 和数值；完整进攻战力修正仍 deferred。 |
| 坚守 | 24 | 4 | 0 | High | P4.6 已识别 profile 和数值；完整防守战力修正仍 deferred。 |
| 壁垒 | 26 | 0 | 0 | High | P4.6 已识别 profile；完整承伤顺序和同优先级选择仍 deferred。 |
| 后排 | 6 | 0 | 0 | High | P4.6 已识别 profile；完整承伤顺序仍 deferred。 |
| 游走 | 38 | 4 | 0 | Medium/High | P4.6 已识别 profile；真实跨战场移动需要多战场目的地与移动权限。 |
| 瞬息 | 21 | 7 | 2 | Medium | P4.3 候选；P2 已记录标签，缺“控制者下个回合开始、得分前摧毁”。 |
| 绝念 | 25 | 0 | 0 | High | P4.9 已识别 profile；离场触发队列和摧毁来源时序仍 deferred。 |
| 预知 | 12 | 0 | 0 | Medium | P4.9 已识别 profile，并标注已审计顶牌回收/不回收代表路径 delegated to P2；广义授予与隐藏信息仍 deferred。 |
| 狩猎 | 14 | 0 | 0 | Medium/High | P4.7 已识别 profile 和数值；P4.10 只覆盖固定打出获得经验，征服/据守事件经验仍 deferred。 |
| 等级 | 15 | 3 | 0 | Medium/High | P4.7 已识别 profile 和阈值；P4.15 已给《踏苔蜥》接入 `等级3` 入场 +1 战力与法盾代表路径，P4.16 已给《风行狐》接入 `等级3` 入场 +1 战力与游走代表路径，P4.17 已给《无极学徒》接入 `等级6` 打出抽 1 代表路径，其他等级条件仍 deferred。 |
| 鼓舞 | 12 | 3 | 0 | Medium | P4.7 已识别 profile；P4.14 已给《诺克萨斯新兵》接入本回合已打出其他卡牌记忆和费用 -2 代表路径，P4.21 已给《崔法利求战者》接入同回合鼓舞自增益代表路径，P4.22 已给《危险二人组》接入同回合鼓舞目标临时战力代表路径，P4.23 已给《垃圾场小霸王》接入同回合鼓舞弃 2 抽 2 代表路径，P4.24 已给《先锋队长》接入同回合鼓舞创建两名 1 战力随从代表路径，其他鼓舞分支仍 deferred。 |
| 法盾 | 47 | 1 | 1 | Medium/High | P4.7 已识别 profile 和税值；P4.12 已接入法术选择敌方场上对象的目标税；技能、每次选取 FAQ 细节和授予/静态法盾仍 deferred。 |
| 待命 | 47 | 6 | 0 | High | P4.9 已识别 profile；face-down、隐藏信息、翻开打出和位置限制仍 deferred。 |
| 回响 | 22 | 2 | 0 | Medium | P4.4 已完成 mana-only optional cost/repeat 模型；复杂额外费用、授予回响和模式重复仍后续拆分。 |
| 伏击 | 18 | 0 | 0 | High | P4.9 已识别 profile；反应战场打出和战场目的地仍 deferred。 |
| 装配 | 51 | 0 | 0 | High | P4.8 已识别 profile；贴附、费用和未激活文本执行仍 deferred。 |
| 灵便 | 6 | 0 | 0 | High | P4.8 已识别 profile；装备反应打出和自动贴附仍 deferred。 |
| 百炼 | 16 | 2 | 0 | High | P4.8 已识别 profile；FAQ 指明的可选装配和贴附边界仍 deferred。 |

## Official Text Anchors

P4.0 选出下一批最小代表，不代表已完成规则执行。

| Candidate | Official card text anchor | Existing evidence/tests | Next action |
|---|---|---|---|
| Draw | `SFD·087/221 先知之兆`：抽三张牌。 | `p2-preflight-play-prophets-omen-draw-stack`；P2 抽牌/燃尽规则已覆盖。 | P4.5 已生成 fixed draw primitive plan；状态写入仍由 P2。 |
| Damage | `OGS·003/024 焚烧`：对一名单位造成 2 点伤害。 | `p2-preflight-play-incinerate-damage-stack` 与致命伤害清理族。 | P4.5 已生成 fixed damage primitive plan；动态伤害继续委托 P2。 |
| Destroy | `OGN·229/298 复仇`：摧毁一名单位。 | `p2-preflight-play-vengeance-destroy-unit-stack`；已有摧毁/放逐替代代表路径。 | P4.5 已生成 destroy target primitive plan；替代/触发另拆。 |
| Stun | `OGN·050/298 符文禁锢`：眩晕一名单位。 | `p2-preflight-play-rune-prison-stun-stack` 与 end-turn expiry fixture。 | P4.5 已生成 `STUNNED` primitive plan；状态写入和到期仍由 P2。 |
| Temp might | `OGN·004/298 顺劈`：让一名单位本回合内获得强攻 3。 | `p2-preflight-play-cleave-overwhelm-attacking-power`；P2 已有 `POWER_MODIFIED_UNTIL_END_OF_TURN` 和清理。 | P4.5 已生成 until-end-of-turn power primitive plan；完整战斗强攻仍另拆。 |
| Move | `OGN·043/298 魅惑妖术` / `OGN·168/298 战或逃`：将战场单位移动到基地。 | P2 已有 `UNIT_MOVED_TO_BASE` 原语。 | P4.5 明确继续 `delegated-to-p2`；多战场目的地与游走权限后续建模。 |
| Recall | `OGN·188/298 祖安保镖`：让战场单位返回所属者手牌。 | P2 已有 `UNIT_RETURNED_TO_HAND` / `EQUIPMENT_RETURNED_TO_HAND`。 | P4.5 明确继续 `delegated-to-p2`；隐藏/控制权边界另拆。 |
| Echo | `SFD·031/221 点沙成兵` / `UNL-061/219 台前作秀`：回响 2，重复法术效果。 | P2 已有 `ECHO` optional cost 和 repeat count 样例。 | P4.4 已把 mana-only 回响接入显式 profile/helper；复杂费用与授予回响继续 deferred。 |
| Ephemeral | `UNL-149/219 蒙面侍者` / `OGN·094/298 精灵召唤`：瞬息会在控制者开始阶段开始时摧毁。 | P2 已记录 `瞬息` 标签；P4.3 新增 turn-start 到期摧毁 fixture。 | 已完成最小到期路径；绝念/贴附/战斗触发另拆。 |
| Swift/Reaction/Haste | `OGN·004/298 顺劈`、`OGN·064/298 风之障壁`、`OGN·001/298 灼焰飞龙`、`UNL-006/219 小鲨鱼`、`OGN·010/298 军团后卫`、`UNL-127/219 树根先生`。 | P2 已有反应优先权窗口和急速不支付额外费用入场路径；P4.13 新增 `p4-play-blazing-drake-haste-ready`，P4.18 新增 `p4-play-baby-shark-haste-ready`，P4.20 新增 `p4-play-legion-rearguard-haste-ready`，P4.25 新增 `p4-play-mr-root-haste-ready`。 | P4.2 已建立权限关键词 profile/timing model；已接入 `顺劈` 法术对决焦点窗口、《灼焰飞龙》《小鲨鱼》《军团后卫》和《树根先生》`HASTE_READY` 代表路径，其他急速牌的彩色资源/活跃分支仍 deferred。 |
| Combat keywords | `OGS·007/024 盖伦`：强攻2、坚守2；`UNL-036/219 变异猫咪`：坚守2、壁垒；`UNL-090/219 乐芙兰`：后排；`SFD·096/221 劳伦特护刃者`：游走。 | P2 已有大量 keyword-unit fixture 记录标签。 | P4.6 已建立 combat keyword profile；完整战斗/移动执行仍 deferred。 |
| Resource keywords | `UNL-100/219 贪食魔沼蛙`：狩猎3；`UNL-047/219 踏苔蜥`：狩猎2、等级3；`UNL-075/219 风行狐`：狩猎2、等级3；`UNL-040/219 无极学徒`：狩猎、等级6；`OGN·012/298 诺克萨斯新兵`：鼓舞费用；`OGN·016/298 危险二人组`：鼓舞目标临时战力；`OGN·020/298 垃圾场小霸王`：鼓舞弃 2 抽 2；`OGN·217/298 崔法利求战者`：鼓舞自增益；`OGN·218/298 先锋队长`：鼓舞创建两名随从；`OGN·013/298 呸呸魄罗`：法盾；`SFD·085/221 奥恩`：法盾2。 | P2 已有 keyword-unit fixture 记录标签或 no-optional 分支；P4.12 新增 `p4-play-incinerate-spellshield-tax`；P4.14 新增 `p4-play-noxian-recruit-encourage-cost-reduction`；P4.15 新增 `p4-play-moss-stepper-level3-spellshield`；P4.16 新增 `p4-play-windrunner-fox-level3-roam`；P4.17 新增 `p4-play-wuji-apprentice-level6-draw`；P4.21 新增 `p4-play-trifarian-gloryseeker-encourage-self-boon`；P4.22 新增 `p4-play-dangerous-duo-encourage-target-temp-might`；P4.23 新增 `p4-play-junkyard-bully-encourage-discard-draw`；P4.24 新增 `p4-play-vanguard-captain-encourage-create-minions`。 | P4.7 已建立 resource keyword profile；P4.12 已执行法术选择敌方场上法盾对象的 mana 目标税；P4.14 已执行《诺克萨斯新兵》鼓舞费用 -2 代表路径；P4.15 已执行《踏苔蜥》`等级3` 入场 +1 与法盾代表路径；P4.16 已执行《风行狐》`等级3` 入场 +1 与游走代表路径；P4.17 已执行《无极学徒》`等级6` 打出抽 1 代表路径；P4.21 已执行《崔法利求战者》鼓舞自增益代表路径；P4.22 已执行《危险二人组》鼓舞目标临时战力代表路径；P4.23 已执行《垃圾场小霸王》鼓舞弃 2 抽 2 代表路径；P4.24 已执行《先锋队长》鼓舞创建两名 1 战力随从代表路径；狩猎征服/据守经验、其他等级条件、其他鼓舞效果、技能目标税和授予/静态法盾仍 deferred。 |
| Equipment keywords | `SFD·033/221 多兰之盾`：装配绿色；`SFD·022/221 长剑`：灵便、装配红色；`SFD·008/221 哨兵好手`：百炼；`SFD·085/221 奥恩`：法盾2、百炼。 | P2 已有装备打出和 no-optional 百炼 fixture，记录装备/武装/灵便/百炼标签。 | P4.8 已建立 equipment keyword profile；贴附、卸除、费用、owner/controller 和自动贴附执行仍 deferred。 |
| Lifecycle remaining | `UNL-081/219 赐面守侍`：待命、瞬息；`UNL-161/219 占卜贝壳`：预知；`OGN·190/298 克格莫`：绝念。 | P4.3 瞬息 fixture；P2 已有预知回收/no-recycle fixture 与绝念静态 fixture。 | P4.9 已建立 lifecycle keyword profile；绝念 trigger queue 和广义预知授予仍 deferred。 |
| Interaction remaining | `OGN·199/298 控潮者`：待命；`UNL-021/219 阴森药剂师`：伏击；`UNL-176a/219 蔚`：伏击。 | P2 已有普通打出/静态 fixture；`回响` 已有 P4.4 mana-only 执行路径。 | P4.9 已建立 interaction keyword profile；待命 face-down 和伏击 reaction battlefield play 仍 deferred。 |
| Basic action remaining | `UNL-103/219 处置命令`：回收；`OGN·102/298 传送门大营救`：放逐并重新打出；`OGN·053/298 秘奥义！慈悲度魂落`：增益；`UNL-158/219 牧人的传家宝`：经验；`UNL-040/219 无极学徒`：等级打出抽牌；`UNL-157/219 严厉军士`：按友方场上单位获得经验。 | P2 已有回收/放逐/增益代表路径；P4.10 新增固定打出获得经验 fixture；P4.17 新增 `p4-play-wuji-apprentice-level6-draw`；P4.19 新增 `p4-play-stern-sergeant-dynamic-experience`。 | P4.10 已执行 `UNL-092/219`、`UNL-034/219`、`UNL-158/219` 的固定获得经验；P4.17 已执行《无极学徒》等级 6 打出抽 1；P4.19 已执行《严厉军士》按友方场上单位数量获得经验；经验消耗/激活技能和其他条件抽牌仍 deferred。 |

## P4.2 Permission Keyword Batch

本阶段完成权限关键词的最小规则化模型，保持小批次接入：

- 新增 `CardPermissionKeywordRules`，把出牌时机判定抽成可单测的 `CardPlayTimingDecision`，并提供 `CardPermissionKeywordProfile` 显式识别 `迅捷` / `反应` / `急速`。
- `反应`：沿用 P2 已验证 `CardBehaviorDefinition.CanPlayDuringPriority` 优先权窗口路径，新增 profile 断言并用 `p2-preflight-play-wind-wall-counter-spell.fixture.json` 保持 conformance 绿色；符文/装备激活反应技能仍不在 P4.2 范围。
- `迅捷`：新增 `CanPlayDuringSpellDuel` registry 开关，只给已验证代表 `OGN·004/298 顺劈` 打开；焦点玩家在 `SPELL_DUEL_OPEN` 且无 stack item 时可打出，之后进入现有 P2 结算链路径。新增 fixture `p4-play-swift-cleave-in-spell-duel-focus.fixture.json`。
- `急速`：从 source unit tags 识别 `急速`，并保留现有不支付额外费用的单位入场路径；P4.13 已将《灼焰飞龙》`HASTE_READY` 代表路径接入现有 `mana + power` 费用模型，P4.18 已将《小鲨鱼》接入同一代表路径，P4.20 已将《军团后卫》接入同一代表路径，P4.25 已将《树根先生》接入同一代表路径，其他急速牌仍以 `recognized-deferred` 标记。
- 本批次没有批量启用全部 `迅捷` 牌，没有改动战斗关键词、待命/伏击、装备激活技能或急速额外费用结算。

## P4.3 Lifecycle Keyword Batch

本阶段完成生命周期关键词的最小执行路径：

- `瞬息`：在 `ResolveTurnStart` 入口中新增开始阶段前置清理，只销毁当前 turn player 控制的 base/battlefield `瞬息` 对象，事件排在 `TURN_START_BEGAN` 后、`RUNES_CALLED` 前。
- 新增 fixture `p4-ephemeral-destroys-controlled-objects-turn-start.fixture.json`，验证当前玩家控制的 `瞬息` 基地/战场对象进入废牌堆，对手控制的 `瞬息` 对象不在本次开始阶段被清理。
- `DestroyedUnitOwnerIdsThisTurn` 会记录本次因 `瞬息` 摧毁的单位拥有者，供现有“本回合单位被摧毁”条件继续复用。
- 本批次没有实现 `绝念` 离场触发、`预知` 新路径、装备贴附下的瞬息卸除顺序、得分触发或战斗清理。

## P4.4 Interaction Keyword Batch

本阶段完成互动关键词的最小小批次，选择风险最低的 `回响` mana-only optional cost/repeat 路径：

- 新增 `CardInteractionKeywordRules`、`CardEchoKeywordProfile`、`EchoOptionalCostNames.Echo`，把“P2 registry 暴露 `EchoManaCost > 0` 且命令选择 `optionalCosts = ["ECHO"]`”建模为显式互动关键词 profile。
- `CoreRuleEngine.TryBuildOptionalCostPlan` 不再内联判断 `"ECHO"`，改为调用 `CardInteractionKeywordRules.TryBuildEchoOptionalCost`；当前行为保持不变：额外支付 `EchoManaCost`，`effectRepeatCount = 2`。
- `UNL-061/219 台前作秀` 用官网卡面 `{{回响}}`、P3 `BehaviorSpec.TemplateIds = echo`、P3 `Cost.OptionalCosts = echo` 和 P2 registry `EchoManaCost = 2` 串起只读规格到执行路径。
- `UNL-007/219 处罚` 继续作为非回响法术拒绝 `ECHO` optional cost 的负例；法力不足时仍由既有费用校验拒绝。
- 复用并重新锁定三条已审计 conformance fixture：`p2-preflight-play-center-stage-echo-draw-stack`、`p2-preflight-play-the-curtain-rises-echo-ready-unit`、`p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base`。
- 本批次没有实现有色回响、弃牌/横置/其他非 mana 回响、装备或单位授予回响、可选择模式的“重复指示”细节，也没有进入待命/伏击 face-down 和隐藏信息系统。

## P4.5 Basic Action Template Primitive Batch

本阶段补齐基础动作 template executor 的小批次测试与边界说明：

- 新增 `BehaviorTemplatePrimitiveExecutor`，在 P3 `BehaviorSpec` + P4.1 `BehaviorTemplateDelegationBridge` 之后，生成只读 primitive plan；该 executor 不修改 `MatchState`，不替换 `CoreRuleEngine`。
- 已支持 primitive plan 的低风险模板：`draw`、`damage`、`destroy`、`stun`、`temp_might`。
- 每个 primitive 都从既有 P2 `CardBehaviorDefinition` 读取参数：抽牌张数、固定伤害、目标范围、摧毁目标、`STUNNED` 状态、临时战力修正和条件。
- `move` 与 `recall` 仍明确返回 `delegated-to-p2`：P2 已有 `UNIT_MOVED_TO_BASE`、`UNIT_RETURNED_TO_HAND` 等真实状态写入，但 P4.5 暂不抽象多战场目的地、隐藏信息、owner/controller 边界。
- `gain_experience`、`assemble`、`echo`、`ambush` 以及动态伤害/替代/触发分支仍不生成 primitive plan，继续按各自关键词或后续系统拆分。
- 对 `眩晕` 卡面 reminder text 造成的 P3 `damage` 粗解析噪声增加保护：`STUNNED` 牌上的零伤害 `damage` 模板不会阻断 `stun` primitive plan。
- 新增 `P4PrimitiveExecutorBuildsBasicActionPlansAndLeavesComplexRoutesDelegated` 覆盖 primitive 与 delegated-only 边界；新增 `P4PrimitiveExecutorRepresentativeFixturesStayGreen` 复用 5 条已审计 fixture 锁定 P2 状态写入继续绿色。

## P4.6 Completion Audit And Combat Keyword Profile

本阶段先做完成审计，结论是 P4 不能标记完成：战斗、资源和装备关键词仍有明显 deferred 项。因此本批次继续做最低风险的战斗关键词 profile，不进入完整战斗模型：

- 新增 `CardCombatKeywordRules` 与 `CardCombatKeywordProfile`，从 P2 source object tags 显式识别 `强攻`、`坚守`、`壁垒`、`后排`、`游走`。
- `强攻` / `坚守` 支持无数字默认 `1`，以及 `强攻2`、`坚守5` 等数值后缀。
- profile status 为 `recognized-deferred`：只表示官方文本、P3 keyword parser 和 P2 registry tag 已对齐；不表示完整战斗伤害、承伤顺序或游走移动权限已完成。
- 代表卡验证：`OGN·210/298 莽莽魄罗`、`OGN·052/298 强强魄罗`、`OGS·007/024 盖伦`、`UNL-036/219 变异猫咪`、`UNL-090/219 乐芙兰`、`SFD·096/221 劳伦特护刃者`。
- 新增 `P4CombatKeywordProfilesMapOfficialTextToRegistryTags` 锁定官方卡面文本到 registry profile；新增 `P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 复用 6 条 keyword-unit fixture 保持 P2 入场标签路径绿色。
- 本批次没有实现进攻/防守战力修正结算、壁垒/后排承伤顺序、游走 MOVE_UNIT 合法性、移动触发得分、战场位置精确选择或战斗伤害分配。

## P4.7 Resource Keyword Profile

本阶段继续资源关键词的低风险 profile 小批次：

- 新增 `CardResourceKeywordRules` 与 `CardResourceKeywordProfile`，从 P3 `BehaviorSpec` 官方文本和 P2 source object tags 识别 `狩猎`、`等级`、`鼓舞`、`法盾`。
- `狩猎` / `法盾` 支持无数字默认 `1`，以及 `狩猎3`、`法盾2` 等数值后缀；`等级` 解析 `等级3>`、`等级6>` 等阈值列表。
- profile status 为 `recognized-deferred`：只表示 P3 parser、官方文本和 P2 registry/tag 已对齐；P4.12 已把法术选择敌方场上对象的法盾目标税接入费用计划，P4.14 已把《诺克萨斯新兵》本回合已打出其他卡牌的鼓舞费用减免接入费用计划，P4.15 已把《踏苔蜥》`等级3` 入场 +1 战力与法盾接入单位入场计划，P4.16 已把《风行狐》`等级3` 入场 +1 战力与游走接入单位入场计划，P4.17 已把《无极学徒》`等级6` 打出抽 1 接入单位结算计划，P4.21 已把《崔法利求战者》同回合鼓舞自增益接入单位结算计划，P4.22 已把《危险二人组》同回合鼓舞目标临时战力接入结算计划，P4.23 已把《垃圾场小霸王》同回合鼓舞弃 2 抽 2 接入结算计划，P4.24 已把《先锋队长》同回合鼓舞创建两名 1 战力随从接入结算计划，但经验获得/消耗、其他等级条件、其他鼓舞效果、技能目标税和授予/静态法盾仍未完整执行。
- 代表卡验证：`UNL-100/219 贪食魔沼蛙`、`UNL-047/219 踏苔蜥`、`UNL-075/219 风行狐`、`UNL-040/219 无极学徒`、`OGN·012/298 诺克萨斯新兵`、`OGN·016/298 危险二人组`、`OGN·020/298 垃圾场小霸王`、`OGN·217/298 崔法利求战者`、`OGN·218/298 先锋队长`、`OGN·013/298 呸呸魄罗`、`SFD·085/221 奥恩`。
- 新增 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags` 锁定官方卡面文本到 profile；`P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 复用已审计 fixture 保持 P2 入场/标签/no-optional 路径绿色，并在 P4.14/P4.15/P4.16/P4.17/P4.21/P4.22/P4.23/P4.24 纳入鼓舞费用减免、自增益、目标临时战力、弃牌抽牌、随从创建与等级入场/抽牌代表 fixture。
- P4.7 当时没有实现法盾目标税支付、每次被选为目标的 FAQ 细节、狩猎征服/据守经验获得、经验消耗、等级阈值动态生效、鼓舞本回合记忆或相关触发；P4.12 随后只补法术目标税的代表执行切片，P4.14 随后只补《诺克萨斯新兵》鼓舞费用减免代表执行切片，P4.15 随后只补《踏苔蜥》`等级3` 入场 +1/法盾代表执行切片，P4.16 随后只补《风行狐》`等级3` 入场 +1/游走代表执行切片，P4.17 随后只补《无极学徒》`等级6` 打出抽 1 代表执行切片，P4.21 随后只补《崔法利求战者》鼓舞自增益代表执行切片，P4.22 随后只补《危险二人组》鼓舞目标临时战力代表执行切片，P4.23 随后只补《垃圾场小霸王》鼓舞弃 2 抽 2 代表执行切片，P4.24 随后只补《先锋队长》鼓舞创建随从代表执行切片。

## P4.8 Equipment Keyword Profile

本阶段继续装备关键词的低风险 profile 小批次，只做识别和边界锁定，不进入 P5 装备大系统：

- 新增 `CardEquipmentKeywordRules` 与 `CardEquipmentKeywordProfile`，从 P3 `BehaviorSpec` 官方文本和 P2 source object tags 识别 `装配`、`灵便`、`百炼`，并同步暴露 `武装` 标签。
- `装配` 只在装备牌自身卡面出现装配关键词时识别；`灵便` 只从装备自身关键词或 P2 equipment tag 识别；`百炼` 从单位 source tag 或卡面行首关键词识别，避免把“授予其他对象关键词”的文本误当成自身关键词。
- profile status 为 `recognized-deferred`：只表示 P3 parser、官方文本和 P2 registry/tag 已对齐；不表示装配费用、贴附/卸除、灵便反应打出自动贴附、百炼可选贴附、控制权或未激活装备文本已完整执行。
- 代表卡验证：`SFD·033/221 多兰之盾`、`SFD·022/221 长剑`、`SFD·008/221 哨兵好手`、`SFD·085/221 奥恩`。
- 新增 `P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags` 锁定官方卡面文本到 profile；新增 `P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen` 复用 5 条已审计 fixture 保持 P2 装备打出/no-optional 百炼路径绿色。
- 本批次没有实现 attach/detach、装备战力持续修正、装配费用支付、贴附目标选择、灵便授予反应、百炼 optional branch、owner/controller 变更或贴附状态下的文本开关。

## P4.9 Completion Audit And Remaining Profiles

本阶段按 active goal 做完成审计。结论：P4 仍不能标记为 goal-complete，因为多个显式能力只有 profile/delegated 边界，还没有真实执行路径；但 P4.9 补齐了之前未覆盖的剩余关键词和基础动作 profile，避免状态文件只靠人工备注。

Prompt-to-artifact checklist：

| Requirement | Evidence | Status |
|---|---|---|
| 权限关键词：迅捷、反应、急速 | `CardPermissionKeywordRules`、`P4PermissionKeywordProfilesMapOfficialTextToRegistryFlags`、`P4SwiftKeywordAllowsCleaveInSpellDuelFocusWindow`、`P4HasteOptionalReadyBranchPaysManaAndPowerForRepresentative`、`P4HasteOptionalReadyBranchPaysManaAndPowerForBabyShark`、`P4HasteOptionalReadyBranchPaysManaAndPowerForLegionRearguard`、`P4HasteOptionalReadyBranchPaysManaAndPowerForMrRoot`、`P4PermissionKeywordsKeepExistingP2FixturesGreen` | Partial：迅捷代表路径可玩，反应 P2 path 可玩，《灼焰飞龙》《小鲨鱼》《军团后卫》和《树根先生》`HASTE_READY` 代表路径可玩；其他急速额外费用仍 deferred。 |
| 战斗关键词：强攻、坚守、壁垒、后排、游走 | `CardCombatKeywordRules`、`P4CombatKeywordProfilesMapOfficialTextToRegistryTags`、6 条 keyword-unit fixture | Profile only：完整战斗伤害/承伤/游走移动 deferred。 |
| 生命周期关键词：瞬息、绝念、预知 | `CardLifecycleKeywordRules`、`P4LifecycleKeywordProfilesMapOfficialTextToRegistryTags`、3 条 representative fixture | Partial：瞬息到期可玩，预知顶牌回收代表路径 delegated to P2，绝念 trigger queue deferred。 |
| 资源关键词：狩猎、等级、鼓舞、法盾 | `CardResourceKeywordRules`、`P4ResourceKeywordProfilesMapOfficialTextToRegistryTags`、`P4SpellshieldTaxAddsManaForEnemySpellTarget`、`P4EncourageCostReductionPaysReducedManaAfterAnotherCardThisTurn`、`P4EncourageSelfBoonGrantsBoonAfterAnotherCardThisTurn`、`P4EncourageTargetTempMightRequiresPriorCardAndTarget`、`P4EncourageDiscardDrawRequiresPriorCardAndTwoHandTargets`、`P4EncourageMinionCreationRequiresPriorCard`、`P4LevelThresholdAppliesMossStepperPowerAndSpellshieldAtThreeExperience`、`P4LevelThresholdAppliesWindrunnerFoxPowerAndRoamAtThreeExperience`、`P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience`、`p4-play-incinerate-spellshield-tax`、`p4-play-noxian-recruit-encourage-cost-reduction`、`p4-play-trifarian-gloryseeker-encourage-self-boon`、`p4-play-dangerous-duo-encourage-target-temp-might`、`p4-play-junkyard-bully-encourage-discard-draw`、`p4-play-vanguard-captain-encourage-create-minions`、`p4-play-moss-stepper-level3-spellshield`、`p4-play-windrunner-fox-level3-roam`、`p4-play-wuji-apprentice-level6-draw`、代表 fixture | Partial：法术选择敌方场上法盾对象的 mana 目标税可玩；《诺克萨斯新兵》鼓舞费用 -2 代表路径可玩；《崔法利求战者》鼓舞自增益代表路径可玩；《危险二人组》鼓舞目标临时战力代表路径可玩；《垃圾场小霸王》鼓舞弃 2 抽 2 代表路径可玩；《先锋队长》鼓舞创建两名 1 战力随从代表路径可玩；《踏苔蜥》`等级3` 入场 +1/法盾代表路径可玩；《风行狐》`等级3` 入场 +1/游走代表路径可玩；《无极学徒》`等级6` 打出抽 1 代表路径可玩；狩猎征服/据守经验、其他等级条件、其他鼓舞效果、技能目标税和授予/静态法盾 deferred。 |
| 互动关键词：待命、回响、伏击 | `CardInteractionKeywordRules`、`P4InteractionKeywordProfilesMapOfficialTextToRegistryTags`、`P4EchoKeywordKeepsExistingP2FixturesGreen`、3 条 remaining fixture | Partial：mana-only 回响可玩，待命/伏击 face-down/reaction battlefield play deferred。 |
| 装备关键词：装配、灵便、百炼 | `CardEquipmentKeywordRules`、`P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags`、5 条 no-attach fixture | Profile only：attach/detach/费用/owner-controller deferred。 |
| 基础动作模板：抽牌、伤害、摧毁、眩晕、移动、召回、回收、放逐、临时战力、增益、经验 | `BehaviorTemplatePrimitiveExecutor`、`CardBasicActionRules`、`P4BasicActionProfilesCoverPrimitiveDelegatedAndDeferredActions`、`P4FixedExperienceGainOnPlayUpdatesControllerExperience`、`P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits`、`P4ExperienceOptionalCostReducesManaAndSpendsExperience`、`P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience`、代表 fixture | Partial：draw/damage/destroy/stun/temp_might primitive；move/recall/recycle/banish/boon delegated to P2 representatives；固定打出获得经验、固定经验额外费用减费、《无极学徒》等级条件抽牌和《严厉军士》动态友方场上单位计数经验可玩；激活/条件经验和更多动态分支 deferred。 |
| 复用 P3 BehaviorSpec/template skeleton | `BehaviorTemplateDelegationBridge`、`BehaviorTemplatePrimitiveExecutor`、baseline tests | Covered for registered templates and representative P2 bridges. |
| 保持 P2/P2.5/P3 绿色 | Latest Validation below | Covered by build/full/conformance/catalog/P4 narrow tests after this batch. |
| 补测试/文档/状态文件并提交 | `CardCatalogBaselineTests`、`ConformanceFixtureRunnerTests`、README、本文件、git commit | Covered for P4.25 once committed. |

P4.9 新增内容：

- 新增 `CardLifecycleKeywordRules` 与 `CardLifecycleKeywordProfile`，覆盖 `瞬息`、`绝念`、`预知` 的实现/委托/暂缓状态。
- 扩展 `CardInteractionKeywordRules`，在既有 `回响` profile 之外补 `待命`、`伏击` 的 interaction profile。
- 新增 `CardBasicActionRules` 与 `CardBasicActionProfile`，把基础动作分为 primitive、delegated-to-P2 和 deferred 三类，补齐 `回收`、`放逐`、`增益`、`经验` 的 P4 状态表达。
- 新增 `P4LifecycleKeywordProfilesMapOfficialTextToRegistryTags`、`P4InteractionKeywordProfilesMapOfficialTextToRegistryTags`、`P4BasicActionProfilesCoverPrimitiveDelegatedAndDeferredActions` 三个 CardCatalog baseline tests。
- 新增 `P4LifecycleKeywordProfilesKeepExistingRepresentativeFixturesGreen`、`P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen`、`P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen`，复用 10 条已审计 fixture。
- 本批次没有实现 `绝念` 触发队列、广义 `预知` 授予、`待命` 正面朝下/翻开、`伏击` 反应战场打出、经验获得/消耗状态、完整战斗或装备贴附系统；P4.10 随后只补固定打出获得经验的低风险切片。

## P4.10 Fixed Experience Gain On Play

本阶段补一个低风险真实执行切片，仍不进入等级、狩猎征服/据守、装配贴附或经验消耗大系统：

- `MatchState` 新增 `PlayerExperience`，conformance fixture schema 新增 `initialState.experience` / `expected.finalState.experience`，玩家 snapshot 暴露 `experience`。
- `CardBehaviorDefinition.GainExperienceOnPlay` 只表示固定数值的“当你打出此牌时获得 N 经验”，在 stack item 结算、源牌入场后追加 `EXPERIENCE_GAINED` 事件并更新控制者经验。
- 已接入代表卡：`UNL-092/219 德玛西亚使节` 获得 1 经验，`UNL-034/219 暖春之使` 获得 2 经验，`UNL-158/219 牧人的传家宝` 获得 1 经验。
- `CardBasicActionRules` 将这些固定经验路径标记为 `delegated-to-P2` / `recognized-covered`；P4.19 随后将 `UNL-157/219 严厉军士` 这类“按友方场上单位数量获得经验”的动态计数代表路径接入。
- 已更新三条已审计 fixture：`p2-preflight-play-demacia-envoy-experience-static`、`p2-preflight-play-spring-messenger-experience-static`、`p2-preflight-play-shepherds-heirloom-weapon-equipment`。
- 本批次没有实现经验消耗、等级阈值、狩猎征服/据守经验、装配消耗经验、获得经验记忆条件、动态友方单位计数或任何 P5 装备贴附系统。

## P4.11 Experience Optional Cost Slice

本阶段继续沿 P4.10 的玩家经验状态补一个低风险费用切片，只覆盖“支付固定经验作为打出额外费用来减少法力费用”的代表路径：

- `CoreRuleEngine` 新增 `SPEND_EXPERIENCE:n` optional cost，先只在 registry 显式配置了 `OptionalExperienceCost` 与 `ManaReductionIfExperiencePaid` 的牌上接受。
- `CardBehaviorDefinition` 新增 `OptionalExperienceCost` / `ManaReductionIfExperiencePaid`，当前只接入 `UNL-178/219 波比` 与 `UNL-178a/219 波比`：支付 3 经验，使打出费用减少 3。
- `COST_PAID` payload 暴露 `experience` 与 `optionalCostManaReduction`，`MatchState.PlayerExperience` 在打出时扣减；经验不足时以 `InsufficientCost` 拒绝且不改变状态。
- 新增 fixture `p4-play-poppy-spend-experience-reduce-cost.fixture.json`，验证 P1 以 3 mana + 3 experience 打出《波比》，结算后 P1 experience 为 0，单位进入基地。
- 新增负向 engine test `CoreRuleEngineRejectsPoppyExperienceOptionalCostWhenExperienceIsInsufficient`。
- `CardBasicActionRules` 将固定经验获得和固定经验额外费用都视为已委托/覆盖的 experience 行为，但仍把 `UNL-164/219 安全检查员` 这类会改变效果分支的经验额外费用保持 deferred。
- 本批次没有实现经验激活技能、经验额外费用改变目标范围、装配消耗经验、等级阈值、获得经验记忆、伏击反应战场打出或壁垒战斗承伤。

## P4.12 Spellshield Target Tax Slice

本阶段补一个资源关键词的真实费用切片，只覆盖“法术选择敌方场上对象”的最小 `法盾` 目标税：

- `CardResourceKeywordRules.SpellshieldTaxFromTags` 复用 P4.7 的 `法盾` / `法盾N` 标签解析，供 `CoreRuleEngine` 费用计划调用。
- `CoreRuleEngine` 在 `PLAY_CARD` 目标合法后、费用支付前计算 `spellshieldTaxMana`：只有非单位/非装备的法术式来源，且目标为敌方 base/battlefield 对象并带 `法盾`/`法盾N` 标签时，才把税值加入总 mana cost。
- `COST_PAID` payload 新增 `spellshieldTaxMana` 与 `spellshieldTaxTargetObjectIds`，代表路径可回放具体由哪些目标触发额外费用。
- 新增 fixture `p4-play-incinerate-spellshield-tax.fixture.json`：`OGS·003/024 焚烧` 选择敌方带 `法盾` 的场上单位时，P1 支付基础 2 mana + 1 mana 目标税，结算后造成 2 点伤害。
- 新增负向 engine test `CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient`，验证只有 2 mana 时不能对敌方 `法盾` 单位打出《焚烧》，且不改状态。
- 本批次没有实现技能选择目标、同一次效果多次选取的 FAQ 全细节、授予/静态法盾、目标税可选支付 UI、法盾与反制/控制权/技能结算链的交互，也不进入 P5 触发替换或 P6 全卡牌批量迁移。

## P4.13 Haste Ready Optional Cost Slice

本阶段回到权限关键词里的 `急速`，先补一个官方代表的可选额外费用分支，不改变全局单位默认入场状态，也不批量启用全部急速牌：

- `CardBehaviorDefinition` 新增 `HasteReadyManaCost` / `HasteReadyPowerCost`，P4.13 先给 `OGN·001/298 灼焰飞龙` 配置 `1 mana + 1 power`，对应官方“额外支付 1 和红色”文本在现有资源模型下的最小代表。
- `CardPermissionKeywordRules` 新增 `HASTE_READY` optional cost helper，并把配置了代表费用的急速 profile 状态改为 `implemented-representative`；其他带 `急速` 标签但未配置费用的牌仍保持 `recognized-deferred`。
- `CoreRuleEngine` 在打出费用计划中接受 `optionalCosts: ["HASTE_READY"]`，将额外 mana/power 加入 `COST_PAID`，并把 optional cost 带到 stack item，供结算时记录本次单位入场来自急速活跃分支。
- 新增 fixture `p4-play-blazing-drake-haste-ready.fixture.json`：P1 以 6 mana + 1 power 打出《灼焰飞龙》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- 新增负向 engine test `P4HasteOptionalReadyBranchRejectsInsufficientPower`，验证缺少 power 时不能支付 `HASTE_READY`，且不改手牌、符文池或结算链。
- 本批次没有重写普通单位入场的活跃/休眠默认规则，没有处理全部彩色符能精确匹配、急速授予、从手牌以外打出获得急速、战场目的地、游走/战斗联动或 P5 触发替换系统。

## P4.14 Encourage Cost Reduction Slice

本阶段继续资源关键词里的 `鼓舞`，只补一个官方代表的费用减免分支，不批量启用所有鼓舞效果：

- `MatchState` 新增 `PlayerCardsPlayedThisTurn`，在成功 `PLAY_CARD` 时递增，并在 `END_TURN` 进入下一回合前清空；snapshot player view 暴露 `cardsPlayedThisTurn`，便于开发期 UI/调试观察。
- `CardCostReductionConditionKinds` 新增 `CONTROLLER_PLAYED_ANOTHER_CARD_THIS_TURN`，由 `CoreRuleEngine.ResolveCostReductionMana` 在费用支付前判断；当前被打出的牌尚未计入，因此 `count > 0` 就表示本回合已打出过其他卡牌。
- `OGN·012/298 诺克萨斯新兵` registry 接入 `CostReductionMana: 2`，对应官方“鼓舞—我的费用减少 2”文本；未满足鼓舞条件时仍按原 P2 fixture 支付基础 4 mana。
- 新增 fixture `p4-play-noxian-recruit-encourage-cost-reduction.fixture.json`：P1 先以 0 mana 打出《暴怒之印》，双方让过后再以 2 mana 打出《诺克萨斯新兵》，`COST_PAID` payload 记录 `baseMana: 4` 与 `costReductionMana: 2`。
- 新增负向 engine test `CoreRuleEngineRejectsNoxianRecruitEncourageReductionWithoutPriorCardThisTurn`，验证没有同回合先前打牌记忆且只有 2 mana 时不能打出《诺克萨斯新兵》，且不改手牌、符文池或结算链。
- 新增 `P4CardsPlayedThisTurnMemoryResetsWhenTurnEnds`，锁定“本回合”记忆在回合结束后清空。
- 本批次没有实现鼓舞的目标选择、活跃入场、弃牌抽牌、额外打出随从、从废牌堆打出、有色费用、技能上的鼓舞条件或 P5/P6 触发队列；这些仍按更小批次拆分。

## P4.15 Level Threshold Source Unit Slice

本阶段继续资源关键词里的 `等级`，只补一个官方代表的入场静态修正，不进入等级费用阶梯、战斗、技能或全局静态系统：

- `CardBehaviorDefinition` 新增 `LevelExperienceThreshold` / `LevelSourceUnitPowerBonus` / `LevelSourceUnitTags`，作为“控制者经验不少于阈值时，源单位入场获得固定战力和标签”的窄配置。
- `CoreRuleEngine.PlaySourceUnitToBase` 在单位入场时读取控制者 `PlayerExperience`；阈值满足时把固定战力加到源单位，并追加配置标签。
- 当前只给 `UNL-047/219 踏苔蜥` 配置 `等级3`：P1 experience >= 3 时入场 `+1` 战力并获得 `法盾` 标签；experience 低于 3 时旧 P2 fixture 仍保持 3 战力、`犬形|狩猎2` 标签。
- 新增 fixture `p4-play-moss-stepper-level3-spellshield.fixture.json`：P1 以 3 experience 打出《踏苔蜥》，双方让过后源单位进入基地成为 4 战力，标签为 `CARD_TYPE:UNIT|法盾|犬形|狩猎2`。
- 新增 `P4LevelThresholdAppliesMossStepperPowerAndSpellshieldAtThreeExperience`，并扩展 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 与 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags`，锁定 P3 official text -> registry -> engine fixture 的证据链。
- 本批次没有实现等级费用减少阶梯、等级改变法术效果、等级激活技能、等级给予全局静态效果、狩猎获得经验、法盾技能目标税、或其他牌的等级分支。

## P4.16 Level Threshold Roam Source Unit Slice

本阶段复用 P4.15 的窄模型，只补另一个官方等级 3 代表，验证等级入场修正不只适用于法盾标签：

- 当前给 `UNL-075/219 风行狐` 配置 `等级3`：P1 experience >= 3 时入场 `+1` 战力并获得 `游走` 标签；experience 低于 3 时旧 P2 fixture 仍保持 3 战力、`犬形|狩猎2` 标签。
- 新增 fixture `p4-play-windrunner-fox-level3-roam.fixture.json`：P1 以 3 experience 打出《风行狐》，双方让过后源单位进入基地成为 4 战力，标签为 `CARD_TYPE:UNIT|游走|犬形|狩猎2`。
- 新增 `P4LevelThresholdAppliesWindrunnerFoxPowerAndRoamAtThreeExperience`，并扩展 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 与 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags`，锁定 P3 official text -> registry -> engine fixture 的证据链。
- 本批次没有实现游走跨战场移动、等级活跃进场、等级费用减少阶梯、等级改变法术效果、等级激活技能、狩猎获得经验或其他牌的等级分支。

## P4.17 Level Threshold Draw Slice

本阶段继续资源关键词和基础动作模板的交界，只补一个“等级满足时打出抽牌”代表，不进入狩猎经验事件、动态经验或广义等级系统：

- `CardBehaviorDefinition` 新增 `LevelDrawOnPlayCount`，表示控制者满足 `LevelExperienceThreshold` 时，在源牌结算过程中让控制者抽固定张数。
- `CoreRuleEngine.ResolveStackItemEffect` 在源牌入场和固定获得经验后检查等级阈值；满足时复用现有 `ApplyDrawToPlayer`，因此燃尽、胜利检查和 `CARD_DRAWN` 事件仍走 P2 已验证抽牌路径。
- 当前给 `UNL-040/219 无极学徒` 配置 `等级6`：P1 experience >= 6 时打出后抽 1；experience 低于 6 时旧 P2 fixture 仍保持 2 战力、`狩猎` 标签且不抽牌。
- 新增 fixture `p4-play-wuji-apprentice-level6-draw.fixture.json`：P1 以 6 experience 打出《无极学徒》，双方让过后源单位进入基地，随后抽 1 张主牌堆顶牌。
- 新增 `P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience`，并扩展 resource/basic-action profile tests 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现狩猎征服/据守获得经验、其他等级阈值、等级费用减少阶梯、等级改变法术效果、等级激活技能或动态条件抽牌。

## P4.18 Haste Ready Second Representative Slice

本阶段继续权限关键词里的 `急速`，补第二个低风险代表，验证 `HASTE_READY` 不是单卡特例，同时不进入彩色符能总系统或完整战斗：

- `UNL-006/219 小鲨鱼` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，复用 P4.13 的 `HASTE_READY` 费用计划。
- 新增 fixture `p4-play-baby-shark-haste-ready.fixture.json`：P1 以 4 mana + 1 power 打出《小鲨鱼》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- 新增 `P4HasteOptionalReadyBranchPaysManaAndPowerForBabyShark`，并扩展 permission profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现彩色资源精确匹配、急速授予、从手牌以外打出获得急速、战场目的地、强攻战斗修正、游走/战斗联动或 P5 触发替换系统。

## P4.19 Dynamic Experience Gain Slice

本阶段继续基础动作模板里的经验获得，只补一个仍处于 `PLAY_CARD` 结算内的动态计数代表，不进入战斗胜利、移动触发或技能激活：

- `CardBehaviorDefinition` 新增 `GainExperienceOnPlayPerFriendlyFieldUnit`，当前只给 `UNL-157/219 严厉军士` 配置 `1`，对应官方“场上每有一名友方单位，便获得 1 经验”。
- `CoreRuleEngine` 在源单位入场后统计控制者基地和战场中的友方 `CARD_TYPE:UNIT` 对象，乘以配置值后复用既有 `GainExperience` / `EXPERIENCE_GAINED` 事件；友方装备、敌方单位和非单位对象不计入。
- 更新原 P2 fixture `p2-preflight-play-stern-sergeant-experience-static`：无其他友方单位时，《严厉军士》自身入场后计为 1 名友方场上单位，因此获得 1 经验。
- 新增 fixture `p4-play-stern-sergeant-dynamic-experience.fixture.json`：P1 已有 2 名友方场上单位和 1 件友方装备，打出《严厉军士》后共 3 名友方单位，获得 3 经验；敌方单位和友方装备不计入。
- 新增 `P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits`，并扩展 basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现狩猎征服/据守经验、战斗/移动触发经验、经验激活技能、经验改变效果或目标范围、装备装配经验消耗、更多动态经验卡牌和 P5 触发队列。

## P4.20 Haste Ready Third Representative Slice

本阶段继续权限关键词里的 `急速`，补第三个低风险代表，验证 `HASTE_READY` 能覆盖同样“额外支付 1 和红色”的不同基础费用单位：

- `OGN·010/298 军团后卫` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“急速（你可以选择额外支付 1 和红色，让我以活跃状态进场。）”。
- 新增 fixture `p4-play-legion-rearguard-haste-ready.fixture.json`：P1 以 3 mana + 1 power 打出《军团后卫》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- 新增 `P4HasteOptionalReadyBranchPaysManaAndPowerForLegionRearguard`，并扩展 permission profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现彩色资源精确匹配、急速授予、从手牌以外打出获得急速、战场目的地、战斗关键词或 P5 触发替换系统。

## P4.21 Encourage Self-Boon Slice

本阶段继续资源关键词里的 `鼓舞`，补一个低风险的“打出时自身增益”代表，不进入目标选择、随从指示物、废牌堆打出或技能鼓舞：

- `StackItemState` 新增 `PlayedAfterAnotherCardThisTurn` 快照位，在 `PLAY_CARD` 入栈时记录控制者打出此牌前是否已打出过其他牌，避免结算时用当前牌自身错误触发鼓舞。
- `OGN·217/298 崔法利求战者` registry 接入 `GrantsBoonToSourceUnit`，并用 `SourceBoonConditionKind: PLAYED_AFTER_ANOTHER_CARD_THIS_TURN` 限定只在鼓舞条件满足时自增益。
- 新增 fixture `p4-play-trifarian-gloryseeker-encourage-self-boon.fixture.json`：P1 先打出 0 费《暴怒之印》，再打出《崔法利求战者》，双方让过后源单位进入基地并获得 `增益` 标签和 +1 战力。
- `p2-preflight-play-trifarian-gloryseeker-no-encourage-unit.fixture.json` 继续覆盖未触发鼓舞时只成为 2 战力 `崔法利` 单位，不获得增益。
- 新增 `P4EncourageSelfBoonGrantsBoonAfterAnotherCardThisTurn`，并扩展 resource/basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现《危险二人组》目标战力修正、《垃圾场小霸王》弃牌抽牌、《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、技能鼓舞或 P5/P6 触发队列。

## P4.22 Encourage Target Temp Might Slice

本阶段继续资源关键词里的 `鼓舞`，补一个低风险的“打出时选择单位并给予本回合战力修正”代表，不进入弃牌抽牌、指示物、废牌堆打出或技能鼓舞：

- `CardBehaviorDefinition` 新增 `TargetCountConditionKind`，当前只接入 `PLAYED_AFTER_ANOTHER_CARD_THIS_TURN`，用于让同一张源牌在鼓舞未触发时要求 0 目标、鼓舞触发时要求 1 目标。
- `CoreRuleEngine` 的目标数量校验与结算前目标数量复核均复用同一个条件；结算阶段使用 `StackItemState.PlayedAfterAnotherCardThisTurn` 快照，避免当前牌自身错误触发鼓舞。
- `OGN·016/298 危险二人组` registry 接入条件目标数、`AnyUnit` + `CARD_TYPE:UNIT` 目标限制和 `PowerModifierAmount: 2`，复用既有 `POWER_MODIFIED_UNTIL_END_OF_TURN` 状态写入。
- 新增 fixture `p4-play-dangerous-duo-encourage-target-temp-might.fixture.json`：P1 先打出 0 费《暴怒之印》，再以一名己方基地单位为目标打出《危险二人组》，双方让过后目标本回合内战力 +2。
- `p2-preflight-play-dangerous-duo-no-encourage-mechanical-unit.fixture.json` 继续覆盖未触发鼓舞时 0 目标入场；`CoreRuleEngineRejectsDangerousDuoEncourageWhenPriorCardButMissingTarget` 覆盖鼓舞已触发但缺少目标时的拒绝路径。
- 新增 `P4EncourageTargetTempMightRequiresPriorCardAndTarget`，并扩展 resource/basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现《垃圾场小霸王》弃牌抽牌、《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、技能鼓舞、目标选择 UI 或 P5/P6 触发队列；其中《垃圾场小霸王》弃牌抽牌已由 P4.23 后续切片覆盖。

## P4.23 Encourage Discard Draw Slice

本阶段先做 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择最低风险的鼓舞弃牌抽牌代表，不进入指示物/废牌堆打出/战斗/装备系统：

- `CardDrawConditionKinds.PlayedAfterAnotherCardThisTurn` 让抽牌判断读取 `StackItemState.PlayedAfterAnotherCardThisTurn` 快照，避免当前牌自身错误触发鼓舞。
- `OGN·020/298 垃圾场小霸王` registry 接入条件目标数、`FriendlyHandCard` 目标限制、弃置 2 张己方手牌和抽 2 张牌；鼓舞未触发时仍要求 0 目标且不抽牌。
- 新增 fixture `p4-play-junkyard-bully-encourage-discard-draw.fixture.json`：P1 先打出 0 费《暴怒之印》，再打出《垃圾场小霸王》并选择两张己方手牌，双方让过后源单位入场，所选手牌进入废牌堆，P1 抽 2。
- `p2-preflight-play-junkyard-bully-no-encourage-mechanical-unit.fixture.json` 继续覆盖未触发鼓舞时 0 目标入场；`CoreRuleEngineRejectsJunkyardBullyEncourageWhenPriorCardButMissingDiscardTargets` 覆盖鼓舞已触发但缺少两个弃牌目标时的拒绝路径。
- 新增 `P4EncourageDiscardDrawRequiresPriorCardAndTwoHandTargets`，并扩展 resource/basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、技能鼓舞、弃牌不足时的 UI 提示细节或 P5/P6 触发队列。

## P4.24 Encourage Minion Creation Slice

本阶段先复核 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择低风险的鼓舞随从创建代表，不进入精确战场目的地、触发队列或全卡牌迁移：

- `CardBehaviorDefinition` 新增 `CreatedBaseUnitTokenConditionKind`，当前只接入 `PLAYED_AFTER_ANOTHER_CARD_THIS_TURN`，让 token 创建读取 `StackItemState.PlayedAfterAnotherCardThisTurn` 快照，避免当前牌自身错误触发鼓舞。
- `OGN·218/298 先锋队长` registry 接入创建两名 1 战力 `随从`、带 `CARD_TYPE:UNIT` 标签的基础单位指示物；鼓舞未触发时仍只作为 3 战力 `精锐` 单位入场。
- 新增 fixture `p4-play-vanguard-captain-encourage-create-minions.fixture.json`：P1 先打出 0 费《暴怒之印》，再打出《先锋队长》，双方让过后源单位入场，并创建两名 1 战力随从指示物到控制者基地。
- `p2-preflight-play-vanguard-captain-no-encourage-elite-unit.fixture.json` 继续覆盖未触发鼓舞时不创建随从；`P4EncourageMinionCreationRequiresPriorCard` 覆盖触发路径，resource profile baseline 与代表 fixture theory 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现卡面“此处”的精确战场目的地、随从作为完整牌组来源、技能鼓舞、《不死军团》废牌堆打出、德莱厄斯活跃/光环或 P5/P6 触发队列。

## P4.25 Haste Ready Fourth Representative Slice

本阶段先复核 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择低风险的急速可选费用第四代表，不进入移动触发经验或彩色符能总系统：

- `UNL-127/219 树根先生` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和紫色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-mr-root-haste-ready.fixture.json`：P1 以基础 2 mana + 额外 1 mana + 1 power 打出《树根先生》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-mr-root-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 1 战力 `仙灵|急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForMrRoot` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现紫色资源精确匹配、移动至战场时获得 2 经验、游走/战斗联动、从手牌以外打出获得急速或 P5/P6 触发队列。

## Risk Layers

低风险，可先做桥接和只读验证：

- `draw`、`damage`、`destroy`、`stun`、`temp_might` 已有 P4.5 primitive plan。
- 已由 P2 手写行为覆盖的 `move` / `recall` 代表路径仍保持 `delegated-to-p2`。
- 目标：证明 P3 `BehaviorSpec` / template skeleton 可以安全定位到现有 `CardBehaviorDefinition`，并在 P4.5 继续保持 `CoreRuleEngine` 主路径不变。

中风险，需要小模型后再接入可玩路径：

- 迅捷、反应；急速的《灼焰飞龙》`HASTE_READY` 代表路径已由 P4.13 接入，《小鲨鱼》`HASTE_READY` 第二代表路径已由 P4.18 接入，《军团后卫》`HASTE_READY` 第三代表路径已由 P4.20 接入，《树根先生》`HASTE_READY` 第四代表路径已由 P4.25 接入，其他急速牌的彩色资源/活跃进场仍需后续小批次
- 瞬息到期、预知最小回收分支
- 回响复杂额外费用、授予回响和模式重复分支
- 法盾目标税的最小支付校验已由 P4.12 覆盖法术选择敌方场上对象；技能、授予/静态法盾和完整 FAQ 细节仍需后续小批次
- 固定数值“打出时获得经验”已由 P4.10 接入；固定经验额外费用减费已由 P4.11 接入；《诺克萨斯新兵》鼓舞费用减免已由 P4.14 接入；《踏苔蜥》`等级3` 入场 +1/法盾已由 P4.15 接入；《风行狐》`等级3` 入场 +1/游走已由 P4.16 接入；《无极学徒》`等级6` 打出抽 1 已由 P4.17 接入；《严厉军士》按友方场上单位数量获得经验已由 P4.19 接入；《崔法利求战者》鼓舞自增益已由 P4.21 接入；《危险二人组》鼓舞目标临时战力已由 P4.22 接入；《垃圾场小霸王》鼓舞弃 2 抽 2 已由 P4.23 接入；《先锋队长》鼓舞创建随从已由 P4.24 接入；经验激活技能、经验改变效果/目标范围、其他动态经验、其他等级分支、其他鼓舞效果仍需后续小批次

高风险，暂不进入 P4.1：

- 强攻、坚守、壁垒、后排的完整战斗承伤/战力修正；P4.6 只完成 profile 识别
- 游走的多战场合法移动和移动触发得分；P4.6 只完成 profile 识别
- 待命、伏击的 face-down/隐藏信息/反应翻开路径
- 装配、灵便、百炼的贴附、费用、未激活文本和 owner/controller 边界；P4.8 只完成 profile 识别
- 绝念和其他离场触发队列

## P4 Part Plan

| Part | Status | Percentage | Notes |
|---|---|---:|---|
| P4.0 审计与状态文档 | Done | 100% | 本文件记录候选、统计、风险分层和下一批边界。 |
| P4.1 基础模板安全桥接 | Done | 100% | 新增 template delegation bridge，覆盖 draw/damage/destroy/stun/temp might，并拒绝未启用 `echo` route；不替换 `CoreRuleEngine`。 |
| P4.2 权限关键词最小模型 | Done | 100% | 迅捷/反应/急速 profile/timing model；`顺劈` spell-duel focus 可玩路径；反应/急速复用并锁定 P2 边界。 |
| P4.3 生命周期/资源低风险小批 | Done | 100% | `瞬息` 当前控制者开始阶段到期摧毁；`预知`/`绝念`/法盾目标税后续另拆。 |
| P4.4 互动关键词一小批 | Done | 100% | `回响` mana-only optional cost/repeat 显式模型；复杂回响、待命、伏击继续 deferred。 |
| P4.5 基础动作 executor 小批测试 | Done | 100% | `draw`/`damage`/`destroy`/`stun`/`temp_might` primitive plan；`move`/`recall` 继续 delegated to P2 handwritten。 |
| P4.6 完成审计与战斗关键词 profile | Done | 100% | 审计确认 P4 尚未完成；新增 `强攻`/`坚守`/`壁垒`/`后排`/`游走` profile，完整战斗执行继续 deferred。 |
| P4.7 资源关键词 profile | Done | 100% | 新增 `狩猎`/`等级`/`鼓舞`/`法盾` profile；P4.12/P4.14/P4.15/P4.16/P4.17/P4.21/P4.22/P4.23/P4.24 后法盾法术目标税、《诺克萨斯新兵》鼓舞费用、《崔法利求战者》鼓舞自增益、《危险二人组》鼓舞目标临时战力、《垃圾场小霸王》鼓舞弃牌抽牌、《先锋队长》鼓舞创建随从、《踏苔蜥》《风行狐》和《无极学徒》等级代表路径已接入，其余资源关键词分支继续 deferred。 |
| P4.8 装备关键词 profile | Done | 100% | 新增 `装配`/`灵便`/`百炼` profile；贴附、费用、自动贴附和 owner/controller 执行继续 deferred。 |
| P4.9 完成审计与剩余 profile 收口 | Done | 100% | 新增 lifecycle/interaction/basic-action profile，明确 P4 goal 尚未完全达成的 deferred 能力。 |
| P4.10 固定获得经验执行切片 | Done | 100% | 新增玩家经验状态、固定 `GainExperienceOnPlay` 执行和 3 条代表 fixture；P4.19 已补《严厉军士》动态计数经验，其他动态经验与经验消耗继续 deferred。 |
| P4.11 经验额外费用减费执行切片 | Done | 100% | 新增 `SPEND_EXPERIENCE:n` optional cost、波比代表 fixture 和经验不足拒绝测试；改变效果/目标的经验费用继续 deferred。 |
| P4.12 法盾目标税执行切片 | Done | 100% | 新增 `spellshieldTaxMana` 费用计划、`法盾`/`法盾N` 标签税值复用、代表 fixture 和费用不足拒绝测试；技能/授予/FAQ 全细节继续 deferred。 |
| P4.13 急速活跃可选费用切片 | Done | 100% | 新增 `HASTE_READY` 代表 optional cost、《灼焰飞龙》fixture 和 power 不足拒绝测试；其他急速牌彩色资源/授予/战场联动继续 deferred。 |
| P4.14 鼓舞费用减免执行切片 | Done | 100% | 新增同回合已打出卡牌记忆、《诺克萨斯新兵》费用 -2 代表 fixture、无先前打牌记忆费用不足拒绝测试和回合结束清空测试；其他鼓舞效果继续 deferred。 |
| P4.15 等级入场修正执行切片 | Done | 100% | 新增 `LevelExperienceThreshold` 源单位入场修正、《踏苔蜥》`等级3` +1/法盾 fixture；其他等级费用/效果/技能分支继续 deferred。 |
| P4.16 等级游走入场修正执行切片 | Done | 100% | 复用 `LevelExperienceThreshold` 源单位入场修正，新增《风行狐》`等级3` +1/游走 fixture；游走移动和其他等级分支继续 deferred。 |
| P4.17 等级条件抽牌执行切片 | Done | 100% | 新增 `LevelDrawOnPlayCount` 源牌结算抽牌、《无极学徒》`等级6` 抽 1 fixture；狩猎经验和其他等级分支继续 deferred。 |
| P4.18 急速活跃第二代表切片 | Done | 100% | 复用 `HASTE_READY` optional cost，新增《小鲨鱼》fixture 和 profile/fixture 测试；彩色资源精确匹配和强攻战斗修正继续 deferred。 |
| P4.19 动态经验获得执行切片 | Done | 100% | 新增 `GainExperienceOnPlayPerFriendlyFieldUnit` 和《严厉军士》动态经验 fixture；战斗/移动触发经验和技能消耗继续 deferred。 |
| P4.20 急速活跃第三代表切片 | Done | 100% | 复用 `HASTE_READY` optional cost，新增《军团后卫》fixture 和 profile/fixture 测试；彩色资源精确匹配和其他急速分支继续 deferred。 |
| P4.21 鼓舞自增益执行切片 | Done | 100% | 新增打出时 `PlayedAfterAnotherCardThisTurn` 快照位、《崔法利求战者》鼓舞自增益 fixture 和 profile/fixture 测试；其他鼓舞分支继续 deferred。 |
| P4.22 鼓舞目标临时战力执行切片 | Done | 100% | 新增条件化目标数量、《危险二人组》鼓舞目标 +2 本回合战力 fixture、缺少目标拒绝测试和 profile/fixture 测试；其他鼓舞分支继续 deferred。 |
| P4.23 completion audit + 鼓舞弃牌抽牌执行切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增条件抽牌、《垃圾场小霸王》鼓舞弃 2 抽 2 fixture、缺少两个弃牌目标拒绝测试和 profile/fixture 测试。 |
| P4.24 completion audit + 鼓舞随从创建执行切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增条件 token 创建、《先锋队长》鼓舞创建两名 1 战力随从 fixture 和 profile/fixture 测试。 |
| P4.25 completion audit + 急速活跃第四代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《树根先生》`HASTE_READY` fixture 和 profile/fixture 测试，移动触发经验继续 deferred。 |
| P4.26 goal completion decision | Pending | 0% | 基于 P4.25 audit 继续补下一个低风险执行切片，或把 high-risk execution gaps 明确移交 P5/P6 后再做 goal completion decision。 |

P4 当前整体进度：按当前 part 计 `26/27 = 96.3%`；P4.1 已验证 `5` 个基础模板可安全委托到现有 P2 手写行为，P4.2 已新增最小权限关键词模型和 `1` 条 `迅捷` 法术对决焦点窗口可玩路径，P4.3 已新增 `瞬息` 开始阶段到期摧毁路径，P4.4 已新增 `回响` mana-only optional cost/repeat 显式模型，P4.5 已新增 `5` 个基础动作 primitive plan 并锁定 `move`/`recall` 继续委托 P2，P4.6 已新增 `5` 个战斗关键词 profile，P4.7 已新增 `4` 个资源关键词 profile，P4.8 已新增 `3` 个装备关键词 profile，P4.9 已新增 lifecycle/interaction/basic-action 剩余 profile，P4.10 已新增固定打出获得经验状态执行，P4.11 已新增固定经验额外费用减费执行，P4.12 已新增法术目标 `法盾` mana 税执行，P4.13 已新增《灼焰飞龙》`HASTE_READY` 急速活跃代表费用执行，P4.14 已新增《诺克萨斯新兵》鼓舞费用减免代表执行，P4.15 已新增《踏苔蜥》`等级3` 入场 +1/法盾代表执行，P4.16 已新增《风行狐》`等级3` 入场 +1/游走代表执行，P4.17 已新增《无极学徒》`等级6` 打出抽 1 代表执行，P4.18 已新增《小鲨鱼》`HASTE_READY` 急速活跃第二代表执行，P4.19 已新增《严厉军士》按友方场上单位数量获得经验执行，P4.20 已新增《军团后卫》`HASTE_READY` 急速活跃第三代表执行，P4.21 已新增《崔法利求战者》鼓舞自增益代表执行，P4.22 已新增《危险二人组》鼓舞目标临时战力代表执行，P4.23 已新增《垃圾场小霸王》鼓舞弃 2 抽 2 代表执行，P4.24 已新增《先锋队长》鼓舞创建两名 1 战力随从代表执行，P4.25 已新增《树根先生》`HASTE_READY` 急速活跃第四代表执行。

## Validation Gate

每个进入 P4 可玩路径的能力都必须补齐：

- 规则证据：至少关联 `docs/rules-evidence-index.md` 中的 PDF/FAQ 条目；关键词默认从 `CORE-260330` p92-p105 rules 800+ 起步，法盾/回响/百炼必须核对 `SOUL-OFAQ-260114` / `SOUL-JFAQ-260114`。
- 官网卡面文本：从 `data/official/card-catalog.zh-CN.json` 或 `BehaviorSpec.OfficialText` 固定代表卡文本。
- Engine test：覆盖状态变化或明确验证 delegated behavior plan。
- Conformance fixture：至少一条 `ConformanceFixtureRunnerTests` 可回放路径，或记录 Java legacy oracle 差异。
- SignalR/Room 或等价 E2E：高风险能力进入可玩路径时补 GameHub/Browser Use smoke；P4.1 的纯桥接可先用 engine/conformance 等价测试。
- 文档状态：同步本文件、必要时同步 `docs/rules-evidence-index.md` / `docs/p2-rules-preflight.md` / README。

## Latest Validation

P4.25 已完成验证：

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`：pass，0 warnings，0 errors
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：pass，1726/1726
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`：pass，1657/1657
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`：pass，19/19
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForMrRoot|FullyQualifiedName~P4PermissionKeywordsKeepExistingP2FixturesGreen|FullyQualifiedName~P4PermissionKeywordProfilesMapOfficialTextToRegistryFlags"`：pass，8/8
- `git diff --check`：pass

## Next Step

进入 P4.26：基于 P4.25 audit 继续补下一个低风险执行切片，或把 high-risk execution gaps 明确移交 P5/P6 后再做 goal completion decision。当前不能标记 P4 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支等仍有明确 deferred 项。
