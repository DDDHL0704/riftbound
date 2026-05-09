# 阶段 3C 卡牌证据：Spell Duel / Battle / ASSIGN_COMBAT_DAMAGE

日期：2026-05-09

阶段：**阶段 3C / E 卡牌覆盖矩阵与 battle damage 证据 overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文是 E 的卡牌覆盖矩阵 / FAQ 证据文档，只服务 3C 的 spell duel、battle、`ASSIGN_COMBAT_DAMAGE` 最小官方化切片。它不实现卡牌效果，不修改服务端、前端或测试，不替代 D 的规则审计，也不把任何 representative route 升级成 full-official。

## 1. 3C 范围与禁区

3C 只标记：

- 依赖 spell duel focus / pass / close / return-to-task 的 functional units。
- 依赖 battle task、attack/defense identity、battle initial stack、battle result / cleanup queue 衔接的 functional units。
- 依赖 `ASSIGN_COMBAT_DAMAGE` payload、damage pool、assignment choices、合法分配与零副作用 reject 的 functional units。
- 3C smoke / fixture pool 可能使用的卡牌功能单元与 FAQ / 规则证据边界。
- 后续适合压测 `ORDER_TRIGGERS` 的 functional units，但 3C 不实现它们。

3C 明确不做：

- 1009 entries / 811 functional units 的 full-official 覆盖。
- 完整 battle lifecycle、完整 spell duel runtime、完整 `ASSIGN_COMBAT_DAMAGE` runtime。
- 完整 `ORDER_TRIGGERS` prompt / ordering / batch runtime。
- 完整 LayerEngine、替代/预防、control freeze/release、hidden-info、barrier/back-row/negative-power 全族矩阵。
- 服务端核心、前端、服务端测试、审计主文档或 `riftbound-dotnet.sln` 的改动。

## 2. 规则与 FAQ 依据

FAQ 证据来源为五份 PDF/FAQ 和仓库规则证据索引；不依赖 `cardQaList`，因为官网快照内 `cardQaList` 当前为 0。

| 域 | 依据 | 3C 用途 |
|---|---|---|
| open/closed state 与优先权 | `CORE-260330` p26-p28 rules 307-313 | 区分普通开放状态、关闭状态、focus / priority 的证据边界。 |
| spell duel | `CORE-260330` p33-p36 rules 333-348；`JFAQ-251023` q3.1-q3.3 | focus player、让过、初始栈 focus 与关闭后返回任务。 |
| battle task | `CORE-260330` p77-p78 rules 454-461；`JFAQ-251023` q2.3-q2.4 | 待处理战斗、战斗前法术对决、attack/defense 身份和 battle stack。 |
| assigning vs dealing damage | `CORE-260330` p62-p63 rule 417；p77-p78 rule 460 | 分配伤害不等于造成伤害，完成分配后同时造成。 |
| assignment constraints | `JFAQ-251023` q6.1-q6.4 | battle damage 来源、barrier、后排、排他性分配要求。 |
| negative power | `SOUL-OFAQ-260114` p19-p20 | 负战力单位 battle damage 输出为 0，但实际战力仍保留。 |
| fixture evidence | `docs/rules-evidence-index.md` | 使用已有 `p4-declare-battle-*`、`p4-play-swift-cleave-in-spell-duel-focus`、Kerplunk / Existential Dread / Skullcrack / Moonrise 等代表 fixture 名称。 |

## 3. 机器矩阵 3C 字段

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- `functionalUnits[].stage3C.evidencePriority`
- `functionalUnits[].stage3C.allowedIn3C`
- `functionalUnits[].stage3C.battleRoles`
- `functionalUnits[].stage3C.riskTags`
- `functionalUnits[].stage3C.sourceScenarios`
- `functionalUnits[].stage3C.useBoundary`
- `stage3CBattleDamage`

3C 统计：

| 项 | 数量 |
|---|---:|
| battle / spell-duel / `ASSIGN_COMBAT_DAMAGE` 广义候选 | 287 |
| spell duel effect-kind FUs | 7 |
| battle / attack / combat effect-kind FUs | 145 |
| damage effect-kind FUs | 60 |
| `ORDER_TRIGGERS` 压测候选 | 55 |
| 本轮显式打 3C 标签 FUs | 34 |
| 3C test deck / fixture pool FUs | 13 |
| 3C high-risk battle / damage FUs | 6 |
| damage math pressure FUs | 5 |
| 3C holdback FUs | 1 |
| 后续 `ORDER_TRIGGERS` 推荐压测 FUs | 10 |

这些数字是风险范围和证据标签，不是实现授权。

## 4. 3C 推荐测试卡组 / Fixture Pool

| FU | 代表卡 | 3C 角色 | 证据优先级 | 边界 |
|---|---|---|---|---|
| `FU-fda6183f9d` | `OGS·007/024` 盖伦 | attacker body / payload guard | P0 | 只作已知 attacker/body fixture，不宣称盖伦 full-official。 |
| `FU-6582231b22` | `UNL-036/219` 变异猫咪 | defender body / payload guard | P0 | 只作 defender/body fixture，不扩张到全战斗模型。 |
| `FU-1fdf2a082a` | `UNL-090/219` 乐芙兰 | alternate body / cleanup candidate | P1 | 只作备选身体和清理候选，不实现触发/last breath。 |
| `FU-44f29ad8f7` | `OGN·004/298` 顺劈 | swift spell duel representative | P0 | 只作窄 spell duel / focus pass 代表，不批量开放迅捷/反应卡。 |
| `FU-4215291160` | `UNL-079/219` 黛安娜 | spell duel static pressure | P1 | 只记录 spell-duel / insight 压力，不实现全静态能力。 |
| `FU-bf350b5796` | `UNL-146/219` 辛德拉 | spell duel echo/static pressure | P1 | 只记录 spell-duel 与 echo 边界。 |
| `FU-50ceb593ab` | `OGN·057/298` 格挡 | defender combat trick | P1 | barrier / steadfast 只作后续分配压力，不关闭全族。 |
| `FU-201e46695b` | `SFD·003/221` 血性冲刺 | attacker combat trick | P1 | echo / overwhelm / power modifier 只作证据边界。 |
| `FU-4e1eb0d231` | `SFD·040/221` 扑咚！ | attacking target legality | P1 | 使用 attacking-unit target / stun reject 证据，不扩张目标系统。 |
| `FU-f9f5c508c0` | `UNL-134/219` 存在焦虑 | attacking enemy target legality | P1 | 使用 stun / return 代表证据，不关闭全隐藏信息或移动链。 |
| `FU-ee886701e4` | `OGN·220/298` 强手裂颅 | battlefield status | P1 | 只作双方战场单位 stun 代表。 |
| `FU-5164c0d190` | `SFD·017/221` 雷霆突降 | attacking target damage | P1 | 只作攻击中目标伤害量压力，不关闭完整 damage runtime。 |
| `FU-4329e00e20` | `UNL-198/219` 月之降临 | negative-power boundary | P1 | 只作负战力 battle damage 输出为 0 的后续回归候选。 |

## 5. 高风险 Battle / Damage FUs

| FU | 代表卡 | 风险 | 3C 处理 |
|---|---|---|---|
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | Top20 #3；battle lifecycle、FAQ、费用相邻风险 | 3C P0 证据核心，仍不完成德莱文全文本。 |
| `FU-964b214448` | `SFD·020/221` 德莱文 | Top20 #4；battle body / FAQ baseline | 3C P0 较简单 baseline，仍不 full-official。 |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | Top20 #5；combat damage 后续移动 | 3C P0 标记 damage assignment 依赖，不完成移动触发链。 |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | Top20 #7；attack trigger、hidden info、trigger ordering | 3C P1 边界，只列入后续 `ORDER_TRIGGERS` 压测。 |
| `FU-1945f6918c` | `SFD·029/221` 雷克塞 | overwhelm / battle damage | 3C P1 压力候选，不关闭关键字全族。 |
| `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | spellshield / stun timing | 3C P1 边界，不关闭 spell duel 全族。 |

## 6. Damage Math 压测候选

这些 functional units 适合后续压测 damage assignment、power modifier、同时造成伤害或负战力，但 3C 不实现完整 runtime：

| FU | 代表卡 | 压测点 |
|---|---|---|
| `FU-0b6332bbf0` | `SFD·145/221` 换换乐 | 交换战力后再分配/造成伤害。 |
| `FU-a9dc3495e1` | `OGN·256/298` 妖异狐火 | 多个战场单位选择与 cleanup 衔接。 |
| `FU-64a7f67581` | `OGN·262/298` 天顶之刃 | 敌方战场目标、stun 与后续伤害边界。 |
| `FU-265c03a141` | `OGS·008/024` 绅士决斗 | 双方按战力互相造成伤害。 |
| `FU-2779c06158` | `OGN·128/298` 决斗 | 简单互相造成伤害 baseline。 |

3C holdback：

| FU | 代表卡 | 原因 |
|---|---|---|
| `FU-b646702ec0` | `OGN·268/298` 弹幕时间 | 3A 只覆盖 `PAY_COST` 最小 runtime；它是非战斗伤害，不进入 3C battle damage assignment。 |

## 7. 后续 ORDER_TRIGGERS 压测清单

以下 functional units 适合在 `ORDER_TRIGGERS` runtime 进入专门阶段后压测；3C 只记录，不实现：

| FU | 代表卡 | 压测点 |
|---|---|---|
| `FU-422b450261` | `SFD·170/221` 雷克塞 | attack trigger、reveal、hidden-info ordering。 |
| `FU-67c6b0186e` | `SFD·049/221` 厄斐琉斯 | weapon selection trigger。 |
| `FU-bf81341dd2` | `OGN·103/298` 拉文布鲁姆学生 | spell trigger。 |
| `FU-5cea85e7c3` | `SFD·128/221` 狂热粉丝 | defense trigger / initial battle stack。 |
| `FU-c170628e3a` | `UNL-065/219` 冰谷弓箭手 | attack trigger with payment / decline pressure。 |
| `FU-16d3a6dd4e` | `SFD·165/221` 戈拉斯克调酒师 | last-breath / cleanup trigger。 |
| `FU-4e2e19359f` | `UNL-179/219` 峡谷先锋 | last-breath move ordering。 |
| `FU-7f4a387b92` | `OGN·056/298` 自适应机器人 | conquer trigger / score ordering。 |
| `FU-c027639a3c` | `OGN·035/298` 薇恩 | conquer recall ordering。 |
| `FU-8dae5c40be` | `OGN·121/298` 提莫 | standby defend trigger / reveal during battle。 |

## 8. 仍阻断 Full-Official 的规则域

P0/P1 仍存在：

- 完整 spell duel lifecycle：所有迅捷/反应/反制链、关闭后任务恢复、复杂 `SPELL_DUEL_ACTION` payload。
- 完整 battle lifecycle：完整 task、attack/defense 身份清理、battle stack、战后结果和 cleanup queue 全路径。
- 正式 `ASSIGN_COMBAT_DAMAGE` runtime：damage pool、assignment choices、barrier/back-row/exclusive constraints、合法/非法提交、零副作用 reject。
- `ORDER_TRIGGERS` runtime：attack/defense/conquer/last-breath/standby trigger 的提示、排序、批处理和失败边界。
- LayerEngine、替代/预防、控制权冻结/释放、隐藏信息、负战力与所有 FAQ 卡牌 adjudication。
- 1009 entries / 811 FUs 的逐项 official text、FAQ、实现、测试闭环。

是否允许进入卡牌效果批量覆盖：**不允许。**
