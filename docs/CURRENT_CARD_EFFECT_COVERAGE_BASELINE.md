# 当前卡牌效果覆盖基线

更新时间：2026-05-11

阶段：**阶段 4C-47 / E 卡牌覆盖矩阵 post-freeze overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文只建立统计口径、只读数据基线、矩阵字段、风险排序和阶段性证据 overlay，不实现或修改任何卡牌效果。阶段 1/2 建立卡牌覆盖基线；阶段 3A/3B/3C/3D 只给最小 runtime / lifecycle / battle-damage / trigger-ordering 切片补证据标签；阶段 4B 冻结 card entry -> functional unit -> oracle/effectId -> evidence/tests/status 矩阵；阶段 4C-1 记录 APNAP `ORDER_TRIGGERS` 保守 controller-block 子集；阶段 4C-2 / 4C-3 记录 Watchful Sentinel 与 Honest Broker 真实 last-breath 入队；阶段 4C-4 记录 Treasure Pile trigger payment；阶段 4C-5 / 4C-6 记录 Starfall damage 造成 visible Watchful Sentinel / Honest Broker state-based cleanup last-breath 入队；阶段 4C-7 记录 Spirit Fire explicit destroy 造成 visible Scouting Warhawk last-breath rune-call 入队；阶段 4C-8 记录 Starfall lethal damage + state-based cleanup 造成 visible Scouting Warhawk last-breath rune-call 入队；阶段 4C-9 记录 Starfall lethal damage + state-based cleanup 造成 visible Sad/Loyal Poro 条件抽牌 last-breath 入队；阶段 4C-10 记录 Starfall lethal damage + state-based cleanup 造成 visible Unsung Hero 强力条件抽二 last-breath 入队；阶段 4C-11 记录 Starfall lethal damage + state-based cleanup 造成 visible surviving friendly Ghostly Centaur 监听另一友方被摧毁并本回合 +2 入队；阶段 4C-12 记录 visible surviving friendly Resonant Soul 监听 owner 本回合首个友方摧毁并抽 1 入队；阶段 4C-13 记录 Ghostly Centaur / Resonant Soul true stack destruction non-cleanup `UNIT_DESTROYED` route migration；阶段 4C-14 记录 Savage Jawfish / 凶残颚鱼 true stack 与 Starfall cleanup 友方摧毁入队获得经验；阶段 4C-15A 记录 Minion token family model / infrastructure marker；阶段 4C-15B 记录 Viktor destroyed non-Minion token trigger 最小代表性 baseline；阶段 4C-16 / 4C-17 记录 Mechanical Trickster / Ironclad Vanguard true stack last-breath trigger enqueue；阶段 4C-18 记录这两个 FU 的 state-based cleanup last-breath trigger enqueue；阶段 4C-19 记录 Kogmaw visible last-breath AoE damage representative route；阶段 4C-20B 记录 Undercover Agent triggered `HAND_CHOICE` prompt 微切片；阶段 4C-21 记录 Sunken Temple / 沉没神庙 authoritative `TRIGGER_PAYMENT` + `PAY_COST` 征服强力单位支付抽牌代表切片；阶段 4C-22 记录 Muddy Dredger / 腐泥疏浚工 visible state-based cleanup Last Breath -> Warhawk token 代表切片；阶段 4C-23 记录 Lux / 拉克丝 high-cost spell temporary power 代表切片；阶段 4C-24 记录 Vayne / 薇恩 visible face-up conquer -> `TRIGGER_PAYMENT` / `PAY_COST` pay 1 return-self 代表切片；阶段 4C-25 记录 Icevale Archer / 冰谷弓箭手 attack payment target-selection 代表切片；阶段 4C-26 记录 Jax / 贾克斯 weapon attach -> `TRIGGER_PAYMENT` / `PAY_COST` pay 1 draw 1 代表切片；阶段 4C-27 记录 Treasure Hunter / 寻宝猎人移动后创建休眠 Gold 装备指示物代表切片；阶段 4C-28 记录 Battle or Flight / 战或逃目标战场单位移回 owner base 与 target guard hardening 代表切片；阶段 4C-29 记录 Gust / 罡风 valid public battlefield unit power <= 3 return-to-owner-hand 与 invalid target guard 代表切片；阶段 4C-30 记录 Hunt the Weak / 狩魂 valid public battlefield unit power <= 3 destroy-target guard 代表切片；阶段 4C-31 记录 Reprimand / 责退 valid public battlefield unit return-to-owner-hand target guard 代表切片；阶段 4C-32 记录 Ride the Wind / 驭风而行 friendly public battlefield unit ready -> owner base movement target guard 代表切片；阶段 4C-33 记录 Charm / 魅惑妖术 enemy public battlefield unit -> owner base movement target guard 代表切片；阶段 4C-34 记录 Isolate / 隔绝 enemy public battlefield unit -> owner base no-draw movement target guard 代表切片；阶段 4C-35 记录 Vengeance / 复仇 public unit destroy target guard 代表切片；阶段 4C-36 记录 Hostile Takeover / 恶意收购 gain-control ready enemy battlefield unit target guard 代表切片；阶段 4C-37 记录 Berserk Impulse / 暴怒冲动 opponent top main-deck unit play target guard 代表切片；阶段 4C-38 记录 Edge of Night / 夜之锋刃 play-equipment / assemble-purple guard 代表切片；阶段 4C-39 记录 Zhonya's Hourglass / 中娅沙漏 play-equipment guard 代表切片；阶段 4C-40 记录 Sea Monster Hook / 海兽钓钩 play-equipment guard 代表切片；阶段 4C-41 记录 Giant Arm Kato / 巨腕加藤 play-keyword-unit guard 代表切片；阶段 4C-42 记录 Time Gate / 预时之门 play-equipment guard 代表切片；阶段 4C-43 记录 Sfur Song / 斯弗尔尚歌 play-equipment guard 代表切片；阶段 4C-44 记录 Akshan / 阿克尚 play-unit guard 代表切片；阶段 4C-45 记录 Switcheroo / 换换乐 battlefield power-swap guard 代表切片；阶段 4C-46 记录 Void Burrower / 虚空遁地兽 `LEGEND_ACTION_DOMAIN` shared-oracle design-gated overlay；阶段 4C-47 记录 Draven / 德莱文 battle body / play-unit guard shared-oracle 代表切片，防止把局部 runtime、非 PLAY_CARD 域或模型前置条件误判为全官方卡牌完成。

## 1. 已读取依据

- `docs/A_MASTER_AGENT_GOAL.md`：最终目标要求 1009 张卡完整映射，但阶段 4 才进入卡牌效果覆盖。
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`：当前主控结论为 `NOT READY`；E 的职责是卡牌效果覆盖、官方文本与 FAQ 证据矩阵。
- `docs/符文战场_服务端核心规则自查文档.md`：单卡审计必须建立“文本 - 结构化效果 - 测试”三方一致性，并覆盖 FAQ。
- `docs/rules-evidence-index.md`：当前已有规则域和 fixture 证据目录；该文件不是 1009 张卡覆盖矩阵。
- `data/official/card-catalog.zh-CN.json`：固定官网快照，`fetchedAt = 2026-04-27`，声明 `total = 1009`。

注意：`docs/CURRENT_RULE_EVIDENCE_TODO.md` 可能由 D/A 或其他 worker 持有；本轮 4B E 不修改、不追加。

## 2. 阶段 1 统计口径

| 口径 | 定义 | 阶段 1 用途 | 阶段 4 要求 |
|---|---|---|---|
| card entry | `data/official/card-catalog.zh-CN.json` 的一个 `.cards[]` 条目；以官方 `id` 和精确 `cardNo` 标识。 | 图鉴、收藏、官方文本、图片和快照完整性统计。 | 1009/1009 都必须有状态、官方文本、oracle 映射或明确阻断原因。 |
| collector no | 官方 `cardNo` 的精确字符串，包括 `·`/`-`、大小写、`a/b`、`*`、`·P` 等后缀。 | 防止把异画、版本、promo 或小写 collector 误归并。 | 精确 `cardNo` 映射必须完整；协议和对象 ID 不得规范化 collector 后缀。 |
| base collector | 只用于分析的归并口径，折叠部分 `a/b`、`*`、`·P` 等后缀。 | 发现可能的异画/版本族。 | 不得作为唯一主键，不得替代精确 collector no。 |
| effect-oracle / functional unit | 仓库 `FunctionalUnitBuilder` 风格的行为等价分组：类型、名称、副标题、颜色、英雄、标签、官方文本、费用、战力、构筑限制等字段一致。 | 估算可以复用的效果实现数量。 | 可以复用实现，但每个 card entry 到 oracle/effectId 的映射必须显式完整。 |
| representative | 代表性规则路径或 fixture 已覆盖某个行为族、关键词族或非 PLAY_CARD 规则域。 | 阶段 1 只能记录为已有代表证据。 | 不得宣称为全官方完成。 |
| full-official | 卡面文本、勘误、FAQ、所有模式/目标/费用/时机/替换/持续效果/失败边界和自动化测试均闭合。 | 阶段 1 不授予。 | 阶段 4 后才可逐项授予；依赖 P0/P1 规则域清零。 |

`BehaviorSpec.ConformanceTier` 中已有 `representative-rule-pass` 与 `full-official-rule-pass` 的区别。阶段 1 必须保持这个区分：代表性通过只能说明“有可追溯实现或样例路径”，不能说明“该卡官方文本全部实现”。

## 3. 官方快照只读统计

### 3.1 总量与集合

| 项 | 数量 |
|---|---:|
| 快照声明总数 | 1009 |
| `.cards[]` 实际条目 | 1009 |
| 唯一 `id` | 1009 |
| 唯一精确 `cardNo` | 1009 |
| 唯一中文名 | 723 |
| 唯一规范化规则文本 | 773 |
| `cardQaList` 非空卡 | 0 |
| `cardQaList` 问答项 | 0 |

`cardQaList` 当前没有 FAQ 内容，因此 FAQ 证据不能从官网快照字段直接得出，必须从五份 PDF/FAQ 建立独立矩阵。

### 3.2 collector 前缀

| 前缀 | 条目数 |
|---|---:|
| OGN | 364 |
| SFD | 312 |
| UNL | 298 |
| OGS | 24 |
| ARC | 6 |
| FND | 5 |

### 3.3 类型

| 类型 | card entries | functional units | 可复用节省 |
|---|---:|---:|---:|
| 单位 | 257 | 255 | 2 |
| 英雄单位 | 235 | 153 | 82 |
| 法术 | 158 | 158 | 0 |
| 传奇 | 106 | 44 | 62 |
| 装备 | 93 | 86 | 7 |
| 战场 | 57 | 54 | 3 |
| 符文 | 48 | 6 | 42 |
| 专属法术 | 34 | 34 | 0 |
| 指示物单位 | 9 | 9 | 0 |
| 专属装备 | 5 | 5 | 0 |
| 专属单位 | 3 | 3 | 0 |
| 指示物战场 | 2 | 2 | 0 |
| 指示物装备 | 2 | 2 | 0 |

汇总：1009 个官方条目按 functional unit 口径为 811 个功能单元；其中 113 个重复功能组覆盖 311 个官方条目，理论上可避免 198 个重复实现。

### 3.4 collector 归并风险

| 项 | 数量 |
|---|---:|
| 精确 collector no | 1009 |
| 分析用 base collector no | 873 |
| base collector 重复组 | 127 |
| base collector 重复条目 | 263 |
| 含小写字母后缀条目 | 91 |
| 含 `*` 变体条目 | 36 |
| 含 `·P` promo 后缀条目 | 4 |

代表例：

| base collector | 精确 collector no | 名称 |
|---|---|---|
| OGN-007/298 | `OGN·007/298`, `OGN·007a/298`, `OGN·007b/298` | 炽烈符文 |
| OGN-193/298 | `OGN·193/298`, `OGN·193a/298`, `OGN·193b/298` | 厄运小姐 |
| SFD-082/221 | `SFD·082/221`, `SFD·082a/221`, `SFD·082b/221·P` | 伊泽瑞尔 |
| OGN-276/298 | `OGN·276/298`, `OGN·276a/298` | 战场变体 |
| OGN-278/298 | `OGN·278/298`, `OGN·278a/298` | 战场变体 |
| OGN-293/298 | `OGN·293/298`, `OGN·293a/298` | 战场变体 |

阶段 1 结论：collector no 必须以官方精确字符串为主键；base collector 只能作为分析辅助。

### 3.5 可能复用的规则文本

| 项 | 数量 |
|---|---:|
| 唯一规范化规则文本 | 773 |
| 重复规则文本组 | 122 |
| 重复规则文本涉及条目 | 301 |
| 重名组 | 114 |
| 重名条目 | 400 |

规则文本重复不等于 full-official 完成。相同文本还必须比较类型、费用、战力、颜色、英雄、标签、勘误和 FAQ 互动，才能合并到同一个 effect-oracle。

代表重复文本组：

| 条目数 | 示例 collector no | 名称/说明 |
|---:|---|---|
| 8 | `OGN·007/298`, `SFD·R01`, `UNL-R01` 等 | 基础符文 functional unit |
| 8 | `OGN·214/298`, `SFD·R06`, `UNL-R06` 等 | 基础符文 functional unit |
| 4 | `OGN·197/298`, `OGN·197a/298`, `OGN·197b/298`, `FND-196/298` | 同名英雄单位复用 |
| 4 | `SFD·057/221`, `SFD·057a/221`, `SFD·225/221`, `SFD·225*/221` | 艾瑞莉娅变体复用 |
| 4 | `SFD·095/221`, `SFD·102/221`, `SFD·108/221`, `SFD·115/221` | 相同装配文本，但仍需比较装备身份和附加效果 |

## 4. 最小覆盖矩阵骨架

阶段 2 已新增机器可读骨架 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。该文件填入 1009 条 snapshot entry 到 811 个 functional unit 的映射，以及当前 representative route / FAQ 候选 / 风险标签，但仍不代表 full-official 完成。

| 矩阵域 | 最小字段 | 阶段 1/2 状态 | 阶段 4 才能完成 |
|---|---|---|---|
| 官方条目身份 | `id`, `cardNo`, `cardName`, `cardCategoryName`, `setPrefix`, `frontImage` | 快照完整，1009/1009 可统计。 | 每个条目都映射到 oracle/effectId 或阻断项。 |
| collector 映射 | `cardNo`, `baseCollectorNo`, `variantKind` | 已发现 127 个 base collector 重复组。 | 所有变体精确映射，不以 base collector 覆盖精确编号。 |
| effect-oracle | `functionalUnitId`, `representativeCardNo`, `memberCardNos`, `officialTextHash` | 811 functional units 基线已确认。 | 每个 unit 绑定实现、测试和 FAQ 状态。 |
| 官方文本 | `officialText`, `textHash`, `errataText?`, `errataSource?` | 官网文本存在；勘误覆盖未建立。 | 所有勘误/FAQ 文本差异逐条标注。 |
| FAQ 证据 | `faqSource`, `pageOrQuestion`, `affectedCardNos`, `rulingSummary` | catalog 内 `cardQaList` 为 0，需从 PDF/FAQ 抽取。 | FAQ 涉及卡牌均有回归测试或人工审计结论。 |
| 实现状态 | `status`, `implementedByCardNo`, `implementedEffectKind`, `conformanceTier` | 只能区分 representative / blocked / manual；不得授予 full-official。 | P0/P1 清零后逐项升级 full-official。 |
| 测试证据 | `fixtureIds`, `successCase`, `failureCase`, `faqCase`, `hiddenInfoCase` | `rules-evidence-index.md` 已有大量 fixture 证据，但不是全卡矩阵。 | 每个复杂 oracle 至少成功 + 失败/边界；FAQ 卡另有 FAQ 回归。 |
| 依赖规则域 | `requiresPayment`, `requiresLayer`, `requiresCleanup`, `requiresBattle`, `requiresSpellDuel` | 仍有 P0/P1 规则域阻断。 | 依赖规则域完成后再批量落单卡。 |

建议阶段 4 实体矩阵文件再拆为机器可读数据与人工审计文档，避免把长表塞进 `rules-evidence-index.md`。

阶段 2 机器可读骨架统计：

| 项 | 数量 |
|---|---:|
| snapshot entries | 1009 |
| functional units | 811 |
| direct card behavior FUs | 694 |
| non PLAY_CARD domain representative FUs | 117 |
| direct card behavior snapshot entries | 785 |
| non PLAY_CARD domain representative snapshot entries | 224 |
| PDF/FAQ 卡名候选 FUs | 179 |
| PDF/FAQ 卡名候选 snapshot entries | 227 |

这些 implementation 数字只表示当前仓库有 representative route 或专门域 route；在 P0/P1 规则域、FAQ adjudication 和逐项测试完成前，`full-official-rule-pass` 仍不得授予。

## 5. 阶段 4 才能做的事项

以下事项不得在阶段 1/2 启动：

1. 不逐张实现 1009 个 card entries。
2. 不把 811 个 functional units 批量写成 `CardBehaviorDefinition`。
3. 不批量修改 `src/**`、测试 fixture 或官方快照。
4. 不实时抓取官网覆盖 2026-04-27 快照。
5. 不把 `representative-rule-pass` 改写为 `full-official-rule-pass`。
6. 不用 base collector no 覆盖精确 collector no。

阶段 4 的进入条件：

1. A 主控确认服务端 P0 规则域已收口，至少包括 cleanup queue、battle/spell duel lifecycle、正式复杂 prompt、PaymentEngine/LayerEngine 关键边界。
2. D/E 已完成 FAQ 抽取计划，明确每个 FAQ 问题是否影响具体卡牌、关键词或通用规则域。
3. 建立 card entry -> functional unit -> effect-oracle -> implementation -> evidence -> tests 的机器可查映射。
4. 明确批量实现的写入锁、测试过滤器、回滚策略和每批最大范围。

## 6. 当前 P0/P1 阻断

P0 仍存在：

- central cleanup queue 未完整官方化。
- spell duel / battle 完整生命周期仍未完成。
- `PAY_COST` 已有 3A 最小 runtime，4C-4 已有 Treasure Pile `TRIGGER_PAYMENT` 代表路径，4C-21 已有 Sunken Temple `TRIGGER_PAYMENT` / `PAY_COST` 征服强力单位支付抽牌代表路径，`ASSIGN_COMBAT_DAMAGE` 已有 3C 最小 runtime，`ORDER_TRIGGERS` 已从 3D 最小 runtime window 升级为 4C-1 保守 APNAP controller-block 子集，4C-2 / 4C-3 / 4C-5 / 4C-6 / 4C-7 / 4C-8 / 4C-9 / 4C-10 / 4C-11 / 4C-12 只验证 Watchful Sentinel、Honest Broker、visible Watchful cleanup、visible Honest Broker cleanup、Scouting Warhawk explicit destroy、Scouting Warhawk cleanup、Sad/Loyal Poro conditional cleanup、Unsung Hero powerful cleanup、Ghostly Centaur friendly-destroyed cleanup 与 Resonant Soul first-friendly-destroyed cleanup 十类触发入队切片；4C-13 只迁移 Ghostly Centaur / Resonant Soul true stack destruction non-cleanup route；4C-14 只验证 Savage Jawfish true stack 与 Starfall cleanup 友方摧毁入队获得经验切片；4C-15A 只降低 token subtype/family/minion-classification 前置 blocker；4C-15B 只关闭 Viktor destroyed non-Minion trigger enqueue 代表性 baseline；4C-16 / 4C-17 只验证 Mechanical Trickster / Ironclad Vanguard true stack last-breath trigger enqueue / priority 代表路径；4C-18 只验证这两个 FU 的 state-based cleanup last-breath trigger enqueue 代表路径；4C-19 只验证 Kogmaw visible last-breath AoE damage representative route；4C-20B 只验证 Undercover Agent triggered hand-choice prompt 微切片；4C-22 只验证 Muddy Dredger visible face-up state-based cleanup Last Breath -> `TriggerQueue` -> `ORDER_TRIGGERS` / stack / priority -> Warhawk `UNL·T02` token 代表路径；完整 PaymentEngine、完整 damage assignment 全规则矩阵、完整 trigger engine 仍未正式完成。
- 正式 18 步 E2E 未最终收口。
- 1009 张官方卡牌效果与 FAQ 证据矩阵未完成。

P1 仍存在：

- PaymentEngine 仍未统一到完整官方支付窗口。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果仍未达到最终完整模型。
- FAQ 涉及卡牌尚未全部抽取成证据项和回归测试。
- 现有代表路径不能作为 full-official 证明。

## 7. 阶段 1 E 汇总

完成项：

- 建立 card entry、collector no、base collector、effect-oracle / functional unit、representative、full-official 的阶段 1 口径。
- 基于 `data/official/card-catalog.zh-CN.json` 完成只读统计。
- 明确 `cardQaList` 当前为 0，FAQ 证据必须来自 PDF/FAQ，而不是官网快照字段。
- 输出最小覆盖矩阵骨架和阶段 4 禁入清单。

新增文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`

未修改文件：

- `docs/CURRENT_RULE_EVIDENCE_TODO.md`：已有未跟踪改动，按锁规则未覆盖。
- `docs/rules-evidence-index.md`
- `data/official/card-catalog.zh-CN.json`
- `src/**`

是否允许进入卡牌效果批量覆盖：**不允许。**

## 35. 阶段 4C-22 E 汇总

阶段 4C-22 名称：Muddy Dredger Last Breath Warhawk representative baseline。E/A 只更新覆盖矩阵与索引证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `UNL-153/219` Muddy Dredger / 《腐泥疏浚工》在冻结矩阵中的真实 FU 为 `FU-b829fb32b9`，snapshot entry id 为 `34701`。
- 当前 oracle/effectId：`MUDDY_DREDGER_LAST_BREATH_WARHAWK_STATIC`；runtime 代表效果 kind 为 `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK`。
- 4B status：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- Token identity：`UNL·T02` Warhawk / 《战鹰》，1 power unit，带 `法盾`。

本批记录：

- visible face-up Muddy Dredger 被 state-based cleanup lethal damage 摧毁后入 `TriggerQueue`。
- 多触发经 `ORDER_TRIGGERS`，排序后入 `StackItems`，priority pass 后 `TRIGGER_RESOLVED`。
- 结算创建一名 Warhawk `UNL·T02` 到 controller base，并记录 `UNIT_TOKEN_CREATED`。
- hidden / face-down / standby / invalid source no enqueue / no leak / no token。
- 只标 `FU-b829fb32b9`；Aphelios / `FU-67c6b0186e` 保留为下一批 high-payoff candidate，不作为 4C-22 完成项。

4C-22 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C22` verified FUs | 1 |
| `stage4C22` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。4C-22 不关闭 true stack Muddy Dredger route、完整 Last Breath family、完整 hidden original visibility、Warhawk “打出”完整语义、Spellshield target tax、FAQ adjudication 或 1009/811 full-official。

A 验证结果：focused 52/52、backend full 3407/3407、frontend build passed、Chrome smoke passed、JSON / diff check passed。

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution 未完成。
- complete Last Breath / destroyed / friendly-destroyed family、simultaneous destruction multiplicity matrix 未完成。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口未完成。
- Spellshield target tax / mandatory additional cost / multi-target tax / insufficient payment regression 未完成。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit 未完成。

是否允许升级 full-official：**不允许。**

## 34. 阶段 4C-21 E 汇总

阶段 4C-21 名称：Sunken Temple trigger payment / conquer powerful unit 微切片。E 只更新覆盖矩阵与风险证据，不修改服务端、前端、审计文档、A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·218/221` Sunken Temple / 《沉没神庙》在冻结矩阵中的真实 FU 为 `FU-05ce012700`。
- 当前 oracle/effectId：`BATTLEFIELD_RULE_DOMAIN`。
- 4B status：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- FAQ/rules candidate：`SOUL-JFAQ-260114 p8`、`SOUL-OFAQ-260114 p15`。

本批记录：

- conquer with a powerful unit -> authoritative `TRIGGER_PAYMENT` window -> `PAY_COST`。
- accept path：pay 1 mana -> draw 1 -> close payment window。
- decline path：no mana spent / no card drawn -> close payment window。
- no powerful unit：no prompt / no mutation。
- invalid or stale payment command：rejected without mutation。
- 只标 `FU-05ce012700`；不标其它 battlefield / conquer / triggered-cost FUs。

4C-21 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C21` verified FUs | 1 |
| `stage4C21` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 15 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。4C-21 不关闭完整 PaymentEngine、完整 conquer / powerful / battlefield lifecycle matrix、LayerEngine effective power edge cases、FAQ adjudication 或 1009/811 full-official。

A 验证结果：focused 13/13、backend full 3404/3404、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 PaymentEngine beyond Treasure Pile / Sunken Temple trigger-payment slices 未完成。
- complete conquer / powerful / battlefield lifecycle matrix 未完成。
- LayerEngine / effective power / temporary modifier edge cases 未完整覆盖。
- battle / spell duel / `ASSIGN_COMBAT_DAMAGE` full lifecycle 仍未完成。
- `SOUL-JFAQ-260114 p8`、`SOUL-OFAQ-260114 p15` 仍需 FAQ adjudication。
- 1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 33. 阶段 4C-20B E 汇总

阶段 4C-20B 名称：Undercover Agent triggered hand-choice prompt 微切片。E 只更新覆盖矩阵与风险证据，不修改服务端、前端、审计文档、A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·178/298` Undercover Agent / 《卧底特工》在冻结矩阵中的真实 FU 为 `FU-6a52b04cb2`。
- 当前 oracle/effectId：`UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`。
- 4B status：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- 规则证据：`CORE-260330 p62` / rule `422.4`。

本批记录：

- visible face-up field source -> Last Breath trigger -> Stack -> `HAND_CHOICE` prompt if 2+ hand -> `CHOOSE_HAND_CARDS` validation -> discard chosen / max possible -> draw two。
- 1/0 hand shortfall by `CORE-260330 p62` / rule `422.4`。
- hidden / face-down / standby source no trigger / no leak。
- automated tests：`UndercoverAgentTriggerTests`。
- A 验证结果：focused Undercover 6/6、backend full 3398/3398、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff / JSON / matrix assertions passed。
- Karthus and other FUs remain unmarked。

4C-20B 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C20B` verified FUs | 1 |
| `stage4C20B` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 15 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| cumulative hand-choice prompt verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。4C-20B 不关闭通用 discard hand-choice engine、其它 hidden hand-choice FUs、完整 trigger engine 或 1009/811 full-official。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH20B_UNDERCOVER_HAND_CHOICE_EVIDENCE.md`

仍存在 P0/P1：

- Karthus optional trigger repeat 未覆盖。
- 通用 discard hand-choice engine 未 full-official。
- 其它 hidden hand-choice FUs 未审计。
- complete trigger engine beyond representative Undercover prompt slice。
- hidden / face-down / standby visibility regression beyond this tested guard。
- 1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 32. 阶段 4C-19 E 汇总

阶段 4C-19 名称：Kogmaw last-breath AoE damage representative route。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint / server audit，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·190/298` Kogmaw / 《克格莫》在冻结矩阵中的真实 FU 为 `FU-af8b05c294`。
- 当前 oracle/effectId：`OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`。
- 4B status：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- FAQ/rules candidate：`JFAQ-251023 p7`。

本批记录：

- visible Kogmaw last-breath AoE damage representative route。
- 路径：visible Kogmaw last-breath source -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `AOE_DAMAGE_RESOLVED` -> `DAMAGE_CLEANUP_RUN`。
- Karthus / Undercover Agent remain unmarked；Kogmaw 也只是 representative, not full-official。

4C-19 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C19` verified FUs | 1 |
| `stage4C19` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 14 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。4C-19 不关闭 Kogmaw FAQ adjudication、完整 AoE damage matrix、完整 trigger engine 或 1009/811 full-official。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH19_KOGMAW_LAST_BREATH_AOE_EVIDENCE.md`

仍存在 P0/P1：

- Kogmaw `JFAQ-251023 p7` FAQ adjudication 仍未 full-official。
- complete trigger engine beyond representative Kogmaw slice。
- full AoE damage target/set/prevention/replacement matrix。
- post-damage cleanup edge cases and simultaneous deaths。
- Karthus / Undercover Agent 未覆盖。
- 1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 31. 阶段 4C-18 E 汇总

阶段 4C-18 名称：Mechanical Trickster + Ironclad Vanguard state-based cleanup last-breath trigger enqueue。E 只更新覆盖矩阵与风险证据，不修改功能代码、测试、前端、一般审计文档，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

本批覆盖：

- `FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster / 《机械戏法师》：`MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- `FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard / 《铁甲先锋》：`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- 路径：state-based cleanup `LETHAL_DAMAGE / UNIT_DESTROYED` -> last-breath trigger queued -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Mechanical creates `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION`; Ironclad creates `UNIT_TOKEN_CREATED x2` robots。
- 不创建不存在 FU，不覆盖 Kogmaw / Karthus / Undercover Agent。

4C-18 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C18` verified FUs | 2 |
| `stage4C18` verified snapshot entries | 2 |
| cumulative real-trigger enqueue verified FUs | 13 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。4C-18 只关闭这两个 FU 的 cleanup-route representative trigger enqueue baseline，不关闭 full trigger engine、full trigger-count matrix 或 1009/811 full-official。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH18_MECHANICAL_IRONCLAD_CLEANUP_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- complete trigger engine beyond these representative cleanup baselines。
- multi-source / multi-destroy / simultaneous trigger multiplicity。
- hidden / face-down original visibility modeling beyond tested guards。
- Kogmaw / Karthus / Undercover Agent 未覆盖。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 30. 阶段 4C-17 E 汇总

阶段 4C-17 名称：Ironclad Vanguard true stack last-breath trigger enqueue。A 修正覆盖矩阵口径并更新证据，不修改前端运行时代码，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 4C-17 验证 `FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard / 《铁甲先锋》。
- trigger effect kind：`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- 路径：true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED x2` robots。
- guard：face-down / standby source no enqueue / no metadata / no token。
- P79 fixture updated to queue / priority semantics。
- Tests：`RealIroncladVanguardLastBreathTriggersOrderAndCreateRobotsThroughStack`、`RealIroncladVanguardHiddenSourcesDoNotEnqueueOrCreateRobots`、`P79IroncladVanguardCreatesTwoRobotsWhenDestroyed updated`；backend full 3384/3384 passed by A。
- 只标 Ironclad Vanguard FU；不覆盖 Kogmaw、Karthus、Undercover Agent，也不覆盖 Ironclad state-based cleanup route。

4C-17 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C17` verified FUs | 1 |
| `stage4C17` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 13 |
| cumulative state-based cleanup trigger enqueue verified FUs | 11 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard。overlay status：`IRONCLAD_VANGUARD_TRUE_STACK_LAST_BREATH_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

4C-17 关闭 true stack representative trigger enqueue baseline，但不覆盖 state-based cleanup route、full trigger engine 或 full-official trigger-count matrix。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH17_IRONCLAD_VANGUARD_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- complete trigger engine beyond Ironclad Vanguard true stack representative baseline。
- Ironclad Vanguard state-based cleanup route 未覆盖；cleanup trigger enqueue verified FUs 保持 11。
- Mechanical Trickster state-based cleanup route 未覆盖。
- multi-source / multi-destroy / simultaneous trigger multiplicity。
- hidden / face-down original visibility modeling beyond tested guards。
- Kogmaw / Karthus / Undercover Agent 未覆盖。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 29. 阶段 4C-16 E 汇总

阶段 4C-16 名称：Mechanical Trickster true stack last-breath trigger enqueue。E 只更新覆盖矩阵与风险证据，不修改功能代码，不修改 D checkpoint / server audit / rules index，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 4C-16 验证 `FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster / 《机械戏法师》。
- effect kind：`MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- 路径：true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION`。
- guard：face-down / standby source no enqueue / no metadata / no token。
- P79 fixture updated to queue / priority semantics。
- Tests：`RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack`、`RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions`、`P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed updated`；backend full 3382/3382 passed by A。
- 只标 Mechanical Trickster FU；不覆盖 Ironclad Vanguard、Kogmaw、Karthus、Undercover Agent。

4C-16 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C16` verified FUs | 1 |
| `stage4C16` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 12 |
| cumulative state-based cleanup trigger enqueue verified FUs | 11 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster。overlay status：`MECHANICAL_TRICKSTER_TRUE_STACK_LAST_BREATH_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

4C-16 关闭 true stack representative trigger enqueue baseline，但不覆盖 state-based cleanup route、full trigger engine 或 full-official trigger-count matrix。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH16_MECHANICAL_TRICKSTER_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- complete trigger engine beyond Mechanical Trickster true stack representative baseline。
- Mechanical Trickster state-based cleanup route 未覆盖；cleanup trigger enqueue verified FUs 保持 11。
- multi-source / multi-destroy / simultaneous trigger multiplicity。
- hidden / face-down original visibility modeling beyond tested guards。
- Ironclad Vanguard / Kogmaw / Karthus / Undercover Agent 未覆盖。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 28. 阶段 4C-15B E 汇总

阶段 4C-15B 名称：Viktor destroyed non-Minion token trigger representative baseline。E 只更新覆盖矩阵与风险证据，不修改功能代码，不修改 D checkpoint / server audit / rules index，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 4C-15B 实现 `FU-b5cb36a5c9` / Viktor destroyed non-Minion token trigger 的最小代表性 baseline。
- 关联 cardNos：`ARC-006/006`、`OGN·246/298`、`OGN·246a/298`。
- 路径：true stack `UNIT_DESTROYED` and Starfall lethal cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` 1-power Zaun minion `OGN·273/298` with `TOKEN_FAMILY:MINION`。
- destroyed target pre-removal filter：unit、same controller / friendly、not source、not `CardObjectTags.MinionTokenFamily`。
- source guard：field、face-up、non-standby、same controller、not removal set。
- no-enqueue guards：destroyed minion target；hidden / face-down / standby / opponent source；source also dying。
- Tests：5 new `RealTriggerQueueTests`；backend full 3380/3380 passed by A。
- 不覆盖 Kogmaw / Karthus / Undercover Agent。

4C-15B 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C15B` verified FUs | 1 |
| `stage4C15B` verified snapshot entries | 3 |
| cumulative real-trigger enqueue verified FUs | 11 |
| cumulative state-based cleanup trigger enqueue verified FUs | 11 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-b5cb36a5c9` / Viktor。overlay status：`VIKTOR_DESTROYED_NON_MINION_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

4C-15B 关闭 representative trigger enqueue baseline，但不关闭 full official trigger-count matrix 或 full trigger engine。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH15B_VIKTOR_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- full official trigger-count matrix for Viktor 未关闭。
- complete trigger engine beyond representative Viktor baseline。
- multi-source / multi-destroy / simultaneous trigger multiplicity。
- hidden / face-down original visibility modeling beyond tested guards。
- Kogmaw / Karthus / Undercover Agent 未覆盖。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 27. 阶段 4C-15A E 汇总

阶段 4C-15A 名称：Minion token family model / infrastructure overlay。E 只更新覆盖矩阵与风险证据，不修改功能代码，不修改 D checkpoint / server audit / rules index，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 4C-15A 是 model / infrastructure overlay，不新增 card effect full-official，不实现 Viktor 本体。
- 新稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- 官方随从 token factory `OGN·271/298`、`OGN·272/298`、`OGN·273/298` 带 marker。
- `CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`；Viktor legend 直接随从创建也同步。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield-held minion 等随从 token 带 marker。
- 普通单位不带；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”不带。
- hidden face-down standby opponent snapshot 不泄漏 tags / cardNo / power。
- A 独立复核：backend full 3375/3375 passed；`git diff --check` passed。

4C-15A 矩阵 overlay 统计：

| 项 | 数量 / 状态 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C15AVerifiedInfrastructure` | true |
| `stage4C15AFullOfficialFunctionalUnits` | 0 |
| `stage4C15AFullOfficialSnapshotEntries` | 0 |
| `stage4C15AFUOverlayTags` | 0 |
| cumulative real-trigger enqueue verified FUs | 10 |
| cumulative state-based cleanup trigger enqueue verified FUs | 10 |
| full-official upgrades | 0 |

Viktor impact：`FU-b5cb36a5c9` 的 token subtype / family / minion-classification 前置 blocker 可被降低或关闭，但 Viktor trigger 未关闭；该 FU 仍为 `SHARED_ORACLE_IMPLEMENTATION` / `NEEDS_ENGINE_SUPPORT`，`fullOfficial=false`。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH15A_MINION_TOKEN_FAMILY_EVIDENCE.md`

仍存在 P0/P1：

- Viktor destroyed non-minion trigger behavior 未由 4C-15A 实现或 full-official。
- complete trigger engine beyond visible verified slices。
- same-source / same-pass / multi-destroy multiplicity and non-minion classification in real trigger contexts。
- hidden / face-down original visibility modeling beyond tested snapshot redaction guards。
- Kogmaw / Karthus / Undercover Agent 未覆盖。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 26. 阶段 4C-14 E 汇总

阶段 4C-14 名称：Savage Jawfish friendly-destroyed stack and cleanup trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 新增 verified FU：`FU-bd94334cc5` / `UNL-129/219` Savage Jawfish / 《凶残颚鱼》。
- 官方文本功能摘要：当另一名友方单位被摧毁时，凶残颚鱼获得 1 经验。
- 路径一：true stack `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED +1`。
- 路径二：Starfall lethal cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED +1`。
- source must remain field, face-up, non-standby, same controller, and not destroyed/removal set。
- hidden face-down / standby / opponent-controlled source 不入队、不泄漏、不获得经验。
- same source same pass multi-destroy trigger multiplicity 仍为 P1/TODO，不是 full-official。
- 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent。
- A 已验证：focused RealTriggerQueue 33/33、backend full 3374/3374、frontend build passed、Chrome smoke passed、Stage3 preflight passed、diff check passed。

4C-14 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C14` verified FUs | 1 |
| `stage4C14` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 10 |
| cumulative state-based cleanup trigger enqueue verified FUs | 10 |
| next-pressure candidate FUs | 9 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-bd94334cc5` / `UNL-129/219`《凶残颚鱼》。overlay status：`FRIENDLY_DESTROYED_STACK_AND_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

同族候选只记录为 next-pressure，未标为已实现：Viktor destroyed-unit token family、Kogmaw / Karthus / Undercover Agent complex last-breath、hidden-origin / simultaneous-condition adjudication。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH14_SAVAGE_JAWFISH_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-14 只覆盖 visible Savage Jawfish true stack / cleanup 入队切片。
- same source same pass multi-destroy trigger multiplicity 仍为 P1/TODO。
- Viktor / Kogmaw / Karthus / Undercover Agent 未覆盖。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-experience metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 25. 阶段 4C-13 E 汇总

阶段 4C-13 名称：Stack destroyed trigger migration 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 本批是 route migration，不新增 unique FU coverage。
- route-upgraded FU：`FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》。
- route-upgraded FU：`FU-c146331876` / `OGN·118/298`《残响之魂》。
- 路径：true stack destruction non-cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Ghostly power +2 / Resonant draw 1。
- cleanup path 仍由 4C-11 / 4C-12 覆盖，并从 old stack helper 排除以避免 duplicate enqueue。
- old P79 immediate compatibility 已移除 / 迁移；P79 现在断言 queue / priority semantics。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不生效。
- 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent；same-source multiple simultaneous deaths full matrix 仍未覆盖。
- A 已验证：focused RealTriggerQueue 30/30、backend full 3370/3370、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

4C-13 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C13` tagged FUs | 2 |
| `stage4C13RouteUpgradedFunctionalUnits` | 2 |
| `stage4C13RouteUpgradedSnapshotEntries` | 2 |
| unique new FU coverage | 0 |
| cumulative real-trigger enqueue verified FUs | 9 |
| cumulative state-based cleanup trigger enqueue verified FUs | 9 |
| next-pressure candidate FUs | 9 |
| full-official upgrades | 0 |

已迁移 route 的 FU：`FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》与 `FU-c146331876` / `OGN·118/298`《残响之魂》。overlay status：`TRUE_STACK_DESTRUCTION_TRIGGER_QUEUE_MIGRATED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

同族候选只记录为 next-pressure，未标为已实现：Viktor destroyed-unit token family、Kogmaw / Karthus / Undercover Agent complex last-breath、hidden-origin / simultaneous-condition adjudication。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-13 只迁移 Ghostly / Resonant true stack destruction route。
- Viktor / Kogmaw / Karthus / Undercover Agent 未覆盖。
- same-source / same-owner multiple simultaneous deaths full-official 裁定未关闭。
- per-turn destroyed owner memory full reset matrix 仍未完整官方化。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-effect metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 24. 阶段 4C-12 E 汇总

阶段 4C-12 名称：Resonant Soul state-based cleanup first-friendly-destroyed draw trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-c146331876` / `OGN·118/298`《残响之魂》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 lethal damage + cleanup source，不因本 overlay 升级 full-official。
- 路径：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Resonant source and owner not already destroyed this turn -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `CARD_DRAWN 1`。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不抽牌。
- source 也在本轮 cleanup removal set 时不入队；owner already in `DestroyedUnitOwnerIdsThisTurn` 时不入队、不抽牌。
- per owner per cleanup pass uses first destroyed unit only；simultaneous multiple units 不是 full-official。
- true stack destruction immediate P79 compatibility 保留，本批不迁移。
- 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent。
- A 已验证：focused RealTriggerQueue 27/27、backend full 3368/3368、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

4C-12 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C12` verified FUs | 1 |
| `stage4C12` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 9 |
| cumulative state-based cleanup trigger enqueue verified FUs | 9 |
| next-pressure candidate FUs | 9 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-c146331876` / `OGN·118/298`《残响之魂》。overlay status：`FIRST_FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

同族候选只记录为 next-pressure，未标为已实现：Viktor destroyed-unit token family、Kogmaw / Karthus / Undercover Agent complex last-breath、hidden-origin / simultaneous-condition adjudication。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-12 只覆盖 visible surviving friendly Resonant cleanup 入队切片。
- Viktor / Kogmaw / Karthus / Undercover Agent 未覆盖。
- simultaneous multiple units first-only full-official 裁定未关闭。
- true stack destruction queued migration 未覆盖。
- per-turn destroyed owner memory full reset matrix 仍未完整官方化。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-draw metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 23. 阶段 4C-11 E 汇总

阶段 4C-11 名称：Ghostly Centaur state-based cleanup friendly-destroyed power trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 lethal damage + cleanup source，不因本 overlay 升级 full-official。
- 路径：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Ghostly source -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `POWER_MODIFIED_UNTIL_END_OF_TURN +2`。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不加战力。
- source 也在本轮 cleanup removal set 时不入队。
- same source 同一轮 cleanup pass 中多个友方同时死亡保守封顶为 1 个 trigger；不是 full-official。
- true stack destruction immediate P79 compatibility 保留，本批不迁移。
- 不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent。
- A 已验证：focused RealTriggerQueue 23/23、backend full 3364/3364、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

4C-11 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C11` verified FUs | 1 |
| `stage4C11` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 8 |
| cumulative state-based cleanup trigger enqueue verified FUs | 8 |
| next-pressure candidate FUs | 10 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》。overlay status：`FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

同族候选只记录为 next-pressure，未标为已实现：Viktor destroyed-unit token family、Resonant Soul first-friendly-destroyed draw、Kogmaw / Karthus / Undercover Agent complex last-breath、hidden-origin / simultaneous-condition adjudication。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-11 只覆盖 visible surviving friendly Ghostly cleanup 入队切片。
- Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent 未覆盖。
- same-source 多个友方同时死亡 full-official 裁定未关闭。
- true stack destruction queued migration 未覆盖。
- LayerEngine / temporary power duration cleanup matrix 仍未完整官方化。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-power metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 22. 阶段 4C-10 E 汇总

阶段 4C-10 名称：Unsung Hero state-based cleanup powerful last-breath draw-2 trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-1701d1d89a` / `SFD·167/221`《无名英雄》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 lethal damage + cleanup source，不因本 overlay 升级 full-official。
- 路径：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Unsung Hero `CardObjectState.Power >= 5` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN x2`。
- `Power < 5` 不入队；hidden / face-down / standby Unsung Hero 不入队、不泄漏、不抽牌。
- 严格边界：只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- A 已验证：focused RealTriggerQueue 21/21、backend full 3361/3361、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

4C-10 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C10` verified FUs | 1 |
| `stage4C10` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 7 |
| cumulative state-based cleanup trigger enqueue verified FUs | 7 |
| next-pressure candidate FUs | 11 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-1701d1d89a` / `SFD·167/221`《无名英雄》。overlay status：`POWERFUL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

同族候选只记录为 next-pressure，未标为已实现：Kogmaw / Karthus / Undercover Agent、destroyed / friendly-destroyed families、hidden-origin / simultaneous-condition / effective-power adjudication。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-10 只覆盖 visible Unsung Hero cleanup 入队切片。
- LayerEngine / effective power / temporary modifier 的强力判定未 full-official。
- battlefield objectLocation 全矩阵与 explicit destroy migration 未覆盖。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-draw metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 21. 阶段 4C-9 E 汇总

阶段 4C-9 名称：Sad/Loyal Poro conditional state-based cleanup trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FUs：`FU-f8bfd5c6f9` / `SFD·036/221`《哀哀魄罗》、`FU-938b749c23` / `UNL-221/219`《哀哀魄罗》、`FU-0415e3b46d` / `UNL-156/219`《忠忠魄罗》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 lethal damage + cleanup source，不因本 overlay 升级 full-official。
- 路径：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Poro condition -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Sad Poro 条件：同位置无其他友方正面非待命单位。Loyal Poro 条件：同位置有其他友方正面非待命单位，且该其他友方不在本轮 cleanup removal set。
- hidden / face-down / standby Poro 不入队、不泄漏、不抽牌；同时死亡落单判定仍不是 full-official。
- A 已验证：focused RealTriggerQueue 21/21、backend full 3358/3358、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

4C-9 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C9` verified FUs | 3 |
| `stage4C9` verified snapshot entries | 3 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 6 |
| cumulative state-based cleanup trigger enqueue verified FUs | 6 |
| next-pressure candidate FUs | 12 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FUs：

- `FU-f8bfd5c6f9` / `SFD·036/221`《哀哀魄罗》：`SAD_PORO_LAST_BREATH_DRAW_1`。
- `FU-938b749c23` / `UNL-221/219`《哀哀魄罗》：`SAD_PORO_LAST_BREATH_DRAW_1`。
- `FU-0415e3b46d` / `UNL-156/219`《忠忠魄罗》：`LOYAL_PORO_LAST_BREATH_DRAW_1`。

overlay status：`CONDITIONAL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。

同族候选只记录为 next-pressure，未标为已实现：Unsung Hero cleanup draw、Kogmaw / Karthus / Undercover Agent、destroyed / friendly-destroyed families、hidden-origin / simultaneous-death condition adjudication。

修改 / 新增文件：

- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- 修改：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 修改：`docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- 修改：`docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- 新增：`docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-9 只覆盖 visible Sad/Loyal Poro conditional cleanup 入队切片。
- 同时死亡落单判定未 full-official，仍需 D/用户 FAQ/rules adjudication。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-draw metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 20. 阶段 4C-8 E 汇总

阶段 4C-8 名称：Scouting Warhawk state-based cleanup trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 lethal damage + cleanup source，不因本 overlay 升级 full-official。
- 路径：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`。
- 4C-7 explicit destroy overlay 保留；本批是同一 FU 的 cleanup overlay。
- A 已验证：focused RealTriggerQueue 11/11、backend full 3352/3352、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

4C-8 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C8` verified FUs | 1 |
| `stage4C8` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 3 |
| cumulative state-based cleanup trigger enqueue verified FUs | 3 |
| next-pressure candidate FUs | 15 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》。overlay status：`STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

同族候选只记录为 next-pressure，未标为已实现：Sad / Loyal Poro cleanup draw、Unsung Hero cleanup draw、Kogmaw / Karthus / Undercover Agent、destroyed / friendly-destroyed families。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-8 只覆盖 visible Scouting Warhawk state-based cleanup 入队切片。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-rune metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 19. 阶段 4C-7 E 汇总

阶段 4C-7 名称：Scouting Warhawk explicit destroy real trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》。
- 支撑源牌：`OGN·256/298`《妖异狐火》，对应 `FU-a9dc3495e1`，只作为 explicit destroy source，不因本 overlay 升级 full-official。
- 路径：`UNIT_DESTROYED -> visible Scouting Warhawk SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED / RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`。
- single-trigger compatibility 保留；没有协议 / 前端变化。
- A 已验证：focused RealTriggerQueue 9/9、backend full 3350/3350、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

4C-7 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C7` verified FUs | 1 |
| `stage4C7` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 3 |
| cumulative state-based cleanup trigger enqueue verified FUs | 2 |
| next-pressure candidate FUs | 15 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》。overlay status：`REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

同族候选只记录为 next-pressure，未标为已实现：Scouting Warhawk state-based cleanup、Sad / Loyal Poro cleanup draw、Unsung Hero cleanup draw、Kogmaw / Karthus / Undercover Agent、destroyed / friendly-destroyed families。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-7 只覆盖 visible Scouting Warhawk explicit destroy 入队切片。
- Scouting Warhawk state-based cleanup enqueue 尚未由 4C-7 覆盖。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-rune metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 18. 阶段 4C-6 E 汇总

阶段 4C-6 名称：Honest Broker state-based cleanup trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-3acf92c924` / `SFD·155/221`《诚实掮客》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 damage source，不因本 overlay 升级 full-official。
- 路径：Starfall damage -> state-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。
- hidden / face-down / standby Honest Broker 不入队、不创建 token，防 trigger metadata 泄漏。
- 4C-3 `stage4C3` overlay 保留不回退。
- A 已验证：focused RealTriggerQueue 6/6、backend full 3348/3348、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

4C-6 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C6` verified FUs | 1 |
| `stage4C6` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative state-based cleanup trigger enqueue verified FUs | 2 |
| next-pressure candidate FUs | 16 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-3acf92c924` / `SFD·155/221`《诚实掮客》。overlay status：`STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

同族候选只记录为 next-pressure，未标为已实现：Sad / Loyal Poro cleanup draw、Scouting Warhawk cleanup rune call、Unsung Hero cleanup draw、Kogmaw / Karthus / Undercover Agent、destroyed / friendly-destroyed families。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-5 / 4C-6 只覆盖 visible Watchful / Honest Broker state-based cleanup 入队切片。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue / no-token metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 17. 阶段 4C-5 E 汇总

阶段 4C-5 名称：state-based cleanup trigger enqueue 最小覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- 代表 FU：`FU-67568b793d` / `OGN·096/298`《警觉的哨兵》。
- 支撑源牌：`OGN·029/298`《星落》，对应 `FU-56d6b01aa1`，只作为 damage source，不因本 overlay 升级 full-official。
- 路径：Starfall damage -> state-based cleanup `LETHAL_DAMAGE` -> visible Watchful `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- hidden / face-down / standby Watchful 不入队，防 trigger metadata 泄漏。
- A 已验证：focused RealTriggerQueue 4/4、backend full 3346/3346、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

4C-5 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C5` verified FUs | 1 |
| `stage4C5` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative state-based cleanup trigger enqueue verified FUs | 1 |
| next-pressure candidate FUs | 12 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-67568b793d` / `OGN·096/298`《警觉的哨兵》。overlay status：`STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

同族候选只记录为 next-pressure，未标为已实现：其他 last-breath、destroyed / friendly-destroyed、hidden / face-down origin visibility、FAQ 相关 cleanup trigger FUs。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-5 只覆盖 visible Watchful state-based cleanup 入队切片。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- hidden / face-down trigger original visibility modeling 仍未完整官方化；本批只验证 no-enqueue metadata leak guard。
- FAQ adjudication / regression、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 16. 阶段 4C-3 E 汇总

阶段 4C-3 名称：Honest Broker last-breath real enqueue 小批覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- B 修改 `src/Riftbound.Engine/CoreRuleEngine.cs` 和 `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`。
- `SFD·155/221`《诚实掮客》对应 `FU-3acf92c924`，registry effect kind 为 `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT`。
- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 扩展到真实多触发路径：`UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED`。
- 单个 Watchful / Honest Broker 仍保留旧即时结算兼容；多个官方化 last-breath 触发同时产生时进入排序窗口。
- A 已验证：focused 13/13、backend full 3339/3339、frontend build passed、Chrome smoke passed、stage3 preflight passed。

4C-3 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C3` verified FUs | 1 |
| `stage4C3` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 2 |
| cumulative real-trigger enqueue verified snapshot entries | 2 |
| next-pressure candidate FUs | 23 |
| full-official upgrades | 0 |

已部分降低 blocker 的本批 FU：`FU-3acf92c924` / `SFD·155/221`《诚实掮客》。overlay status：`REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

累计 verified FUs：`FU-67568b793d` / `OGN·096/298`《警觉的哨兵》、`FU-3acf92c924` / `SFD·155/221`《诚实掮客》。

同族候选只记录为 next-pressure，未标为已实现：destroyed / friendly-destroyed、其他 last-breath、on-play registered trigger、attack / defense / conquer trigger FUs。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-2 / 4C-3 只覆盖 Watchful Sentinel 与 Honest Broker 两个 last-breath 入队切片。
- 其他 last-breath / destroyed / friendly-destroyed functional units 尚未逐项验证。
- state-based cleanup trigger enqueue、trigger payment / decline / payment failure 仍未关闭。
- FAQ adjudication 与 ruling-backed tests 仍未覆盖 1009 snapshot entries / 811 functional units。
- `FU-3acf92c924` 仍保留 `NEEDS_ENGINE_SUPPORT`，不得升级 full-official。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 15. 阶段 4C-2 E 汇总

阶段 4C-2 名称：real card-trigger enqueue 最小切片覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- B 修改 `src/Riftbound.Engine/CoreRuleEngine.cs`，新增 `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`。
- `OGN·096/298`《警觉的哨兵》对应 `FU-67568b793d`，registry effect kind 为 `WATCHFUL_SENTINEL_PLAY_UNIT`。
- 多个 Watchful Sentinel 在真实 `UNIT_DESTROYED` 路径中产生 `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`，进入 `TriggerQueue -> ORDER_TRIGGERS prompt -> StackItems -> pass priority -> TRIGGER_RESOLVED / CARD_DRAWN`。
- 单个 Watchful Sentinel 仍保留旧即时结算兼容路径。
- 新测试覆盖跨控制者 APNAP 默认 `orderedTriggerIds` 可直接提交 accepted、非法跨控制者排序拒绝且 no mutation。
- A 已验证：focused 11/11、backend full 3338/3338、frontend build passed、Chrome smoke passed、stage3 preflight passed。

4C-2 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C2` verified FUs | 1 |
| `stage4C2` verified snapshot entries | 1 |
| next-pressure candidate FUs | 24 |
| full-official upgrades | 0 |

已部分降低 blocker 的 FU：`FU-67568b793d` / `OGN·096/298`《警觉的哨兵》。overlay status：`REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

同族候选只记录为 next-pressure，未标为已实现：destroyed / friendly-destroyed、其他 last-breath、on-play registered trigger、attack / defense / conquer trigger FUs。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

仍存在 P0/P1：

- 完整 trigger engine 仍未关闭；4C-2 只覆盖 Watchful Sentinel 真实遗言抽牌触发入队最小切片。
- 其他 last-breath / destroyed-family functional units 尚未逐项验证。
- trigger payment / decline / payment failure、state-based cleanup trigger enqueue 仍未关闭。
- FAQ adjudication 与 ruling-backed tests 仍未覆盖 1009 snapshot entries / 811 functional units。
- `FU-67568b793d` 仍保留 `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW`，不得升级 full-official。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 14. 阶段 4C-1 E 汇总

阶段 4C-1 名称：APNAP `ORDER_TRIGGERS` / battle initial stack / hidden trigger metadata redaction 覆盖矩阵 overlay。E 只更新覆盖矩阵与风险证据，不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

B/A 已提供的新事实：

- `ORDER_TRIGGERS` 从 3D 最小 runtime window 升级为保守 APNAP controller-block 子集。
- prompt metadata 中 `triggerIds` 表示 raw queue order，`orderedTriggerIds` 表示服务端推荐的合法 APNAP resolution top-first 默认提交顺序。
- `legalOrderingConstraints` 记录 `orderingPolicy=APNAP_CONTROLLER_BLOCKS_CONSERVATIVE`、`orderedTriggerIdsSemantics=STACK_RESOLUTION_ORDER_TOP_FIRST`、`controllerBlockOrder`、`legalResolutionControllerBlockOrder`、`crossControllerReorderingAllowed=false`、`withinControllerReorderingAllowed=true`。
- runtime 校验跨控制者 block 非法重排会零副作用失败；同控制者 block 内重排合法；合法排序进入 `StackItems` / stack priority。
- active battle 的 attacker / defender 初始触发已有代表测试进入 `ORDER_TRIGGERS`，再合法排序进入 stack priority。
- 不可见 face-down standby trigger source 在 trigger prompt / snapshot 中按 viewer 脱敏。
- A 后端 full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3337/3337。

4C-1 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `ORDER_TRIGGERS` dependency FUs | 67 |
| `stage4C1` tagged FUs | 67 |
| Top20 中 blocker 被部分降低的 FUs | 6 |
| battle initial stack pressure FUs | 31 |
| hidden trigger metadata redaction candidates | 11 |
| tagged FUs 中仍需 FAQ adjudication | 18 |
| full-official upgrades | 0 |

Top20 中 blocker 被部分降低但仍非 full-official 的 FUs：`FU-104211dbbc`、`FU-2dca1ad450`、`FU-964b214448`、`FU-05ce012700`、`FU-422b450261`、`FU-813144e7d4`。

修改 / 新增文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- `docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_EVIDENCE.md`

仍存在 P0/P1：

- 完整 trigger engine、真实卡牌触发全规则入队、trigger payment / decline / payment failure 仍未关闭。
- 完整 APNAP 多玩家独立排序、保守 controller-block 子集之外的复杂跨控制者排序、battle initial stack 全规则仍未关闭。
- FAQ adjudication 与 ruling-backed tests 仍未覆盖 1009 snapshot entries / 811 functional units。
- 完整 PaymentEngine、完整 `ASSIGN_COMBAT_DAMAGE` 全规则矩阵、spell duel / battle 全生命周期、LayerEngine、替代/预防、隐藏信息仍未 full-official。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**

## 8. 阶段 2 E 汇总

完成项：

- 新增 1009 snapshot entries -> 811 functional units 的机器可读矩阵骨架。
- 为每个 snapshot entry 建立 `cardId/cardNo/baseCollectorNo/functionalUnitId/implementationStatus/faqRefs/uncoveredEffectCategories` 字段。
- 为每个 functional unit 建立 `representativeCardNo/cardNos/implementation/faqRefs/rulesRefs/dependsOnP0RuleDomains/riskScore` 字段。
- 从五份 PDF/FAQ 逐页抽取卡名命中候选；不依赖 `cardQaList`。
- 输出前 20 个最高风险 functional units，供后续阶段先做人工 adjudication、实现和测试。

新增文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`

未修改文件：

- `data/official/card-catalog.zh-CN.json`
- `docs/rules-evidence-index.md`
- `src/**`

是否允许进入卡牌效果批量覆盖：**不允许。**

## 9. 阶段 3A E 汇总

阶段 3A 目标：只服务 Smoke 基线、强类型复杂命令解析、`PAY_COST` 最小 runtime 切片、对战桌面外壳；不进入 1009 张卡全量实现，不修改核心规则引擎。

完成项：

- 在机器矩阵中新增 `stage3A` / `stage3ASmokePayCost` overlay，补充 3A functional units 的 `evidencePriority`、`faqCandidate`、`riskTags`、`allowedIn3A`、`useBoundary`。
- 将 `OGN·268/298`《弹幕时间》标为 3A `PAY_COST` 最小 runtime P0 代表，只覆盖 typed power、`SPEND_POWER`、`RECYCLE_RUNE` 支付资源、`COST_PAID` envelope 与 prompt stamp 边界。
- 将符文资源域 `FU-0ec69ae7e6`、`FU-39041f4562` 和 body fixture `SFD·125/221` 标为 3A PAY_COST 支撑 functional units。
- 将正式开局 smoke observed hand 候选 `ARC-003/006`、`OGN·006/298`、`OGN·009/298` 标为 smoke-only，不进入 spell duel / damage runtime。
- 将 `SFD·202/221`、`SFD·022/221`、`SFD·143/221`、完整 battle/damage/order/runtime、装备/LayerEngine 链与 Top20 高风险卡明确标为 3A holdback。
- 新增 3A Smoke / PAY_COST 证据文档，明确所有卡牌仍是 representative 3A evidence，不是 full-official 覆盖。

新增文件：

- `docs/CURRENT_CARD_EFFECT_STAGE3A_SMOKE_PAY_COST_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`

未修改文件：

- `data/official/card-catalog.zh-CN.json`
- `docs/rules-evidence-index.md`
- `src/**`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`

仍存在 P0/P1：

- battlefield / standby / control / held / conquer task lifecycle 仍未 full-official。
- central cleanup queue、PaymentEngine、LayerEngine、battle / spell duel 完整生命周期仍未清零；3A 只取 `PAY_COST` 最小 runtime 切片。
- 1009 snapshot entries / 811 functional units 的 FAQ adjudication 与测试矩阵仍未完成。

是否允许进入卡牌效果批量覆盖：**不允许。**

## 10. 阶段 3B E 汇总

阶段 3B 名称：Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 最小官方化切片。E 只维护卡牌覆盖矩阵与 FAQ/规则证据，不修改核心规则引擎，不进入 1009 张卡 full-official 覆盖。

完成项：

- 在机器矩阵中新增 `stage3B` / `stage3BBattlefieldLifecycle` overlay，补充 3B selected functional units 的 `lifecycleRoles`、`evidencePriority`、`allowedIn3B`、`riskTags`、`useBoundary`。
- 标注 3B 依赖统计：649 个 lifecycle/cleanup 广义候选、520 个 3B core 候选、287 个 battle/spell-duel 候选、282 个 cleanup 候选、286 个 control/zone/movement 候选、54 个 battlefield rule-domain FUs、12 个 standby 命名或 effect-kind 候选、44 个 legend action domain FUs。
- 将 `FU-05ce012700`《沉没神庙》、`FU-00ee09c2cc`《恶意收购》、`FU-813144e7d4`《战或逃》标为 3B P0 lifecycle 证据核心，但仍不是 full-official 单卡完成。
- 将待命、征服/据守、战场 FAQ 和 cleanup/control 支撑候选标为 P1：`FU-8dae5c40be`、`FU-e3dcc3b30f`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-90673ef9fd`、`FU-6c99fc0e2e`、`FU-d18ac7cbec`、`FU-95b4531e4e`。
- 输出 3B 测试卡组 / fixture pool 姿态，明确它只服务 battlefield lifecycle 证据，不是完整官方卡组或 full-official card pass。
- 输出后续 battle / damage assignment 压测清单，但不实现。

新增文件：

- `docs/CURRENT_CARD_EFFECT_STAGE3B_BATTLEFIELD_LIFECYCLE_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`

未修改文件：

- `data/official/card-catalog.zh-CN.json`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `src/**`
- `src/Riftbound.DevUi/**`

仍存在 P0/P1：

- central cleanup queue 仍需由服务端规则实现与测试证明全路径 enqueue / repeat-until-stable。
- battle / spell duel lifecycle、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 仍未 full-official。
- 控制权冻结/释放、失控待命移除、征服/据守得分触发与替代仍需后续实现/测试闭合。
- 1009 snapshot entries / 811 functional units 的 FAQ adjudication 与 full-official 测试矩阵仍未完成。

是否允许进入卡牌效果批量覆盖：**不允许。**

## 11. 阶段 3C E 汇总

阶段 3C 名称：Spell duel / Battle / `ASSIGN_COMBAT_DAMAGE` 最小官方化证据 overlay。E 只标记依赖关系、测试卡组边界、FAQ/规则证据与后续压测候选，不修改核心规则引擎，不进入 1009 张卡 full-official 覆盖。

完成项：

- 在机器矩阵中新增 `stage3C` / `stage3CBattleDamage` overlay，补充 3C selected functional units 的 `battleRoles`、`evidencePriority`、`allowedIn3C`、`riskTags`、`useBoundary`。
- 标注 3C 依赖统计：287 个 battle / spell-duel / `ASSIGN_COMBAT_DAMAGE` 广义候选、7 个 spell-duel effect-kind FUs、145 个 battle / attack / combat effect-kind FUs、60 个 damage effect-kind FUs、55 个后续 `ORDER_TRIGGERS` 压测候选。
- 将 `FU-fda6183f9d`《盖伦》、`FU-6582231b22`《变异猫咪》、`FU-44f29ad8f7`《顺劈》、`FU-104211dbbc` / `FU-964b214448`《德莱文》、`FU-2dca1ad450`《伊泽瑞尔》标为 3C P0 证据核心，但仍不是 full-official 单卡完成。
- 将 combat trick、战场状态、负战力 / 伤害数学、spell duel static、battle trigger 等候选标为 P1/P2 支撑或压测边界；`FU-b646702ec0`《弹幕时间》继续作为非战斗伤害 holdback，不进入 3C battle damage assignment。
- 输出后续 `ORDER_TRIGGERS` 压测清单：`FU-422b450261`、`FU-67c6b0186e`、`FU-bf81341dd2`、`FU-5cea85e7c3`、`FU-c170628e3a`、`FU-16d3a6dd4e`、`FU-4e2e19359f`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-8dae5c40be`；3C 只记录，不实现。

新增文件：

- `docs/CURRENT_CARD_EFFECT_STAGE3C_BATTLE_DAMAGE_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`

未修改文件：

- `data/official/card-catalog.zh-CN.json`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `src/**`
- `src/Riftbound.DevUi/**`
- `riftbound-dotnet.sln`

仍存在 P0/P1：

- spell duel 完整 lifecycle、完整 battle task、完整 `ASSIGN_COMBAT_DAMAGE` choices / constraints 全规则矩阵仍未 full-official；3C 只关闭最小 runtime。
- `ORDER_TRIGGERS` runtime、attack/defense initial stack ordering、last-breath / conquest / standby trigger prompt 仍未进入 3C。
- LayerEngine、替代/预防、控制冻结/释放、隐藏信息、负战力 / barrier / back-row 全族矩阵仍需后续阶段补实现和测试。
- 1009 snapshot entries / 811 functional units 的 FAQ adjudication 与 full-official 测试矩阵仍未完成。

是否允许进入卡牌效果批量覆盖：**不允许。**
## 12. 阶段 3D E 汇总

阶段 3D 名称：卡牌覆盖矩阵 / complex prompt dependency / `ORDER_TRIGGERS` 证据 overlay。3D 当时关闭 `ORDER_TRIGGERS` 最小 runtime window：prompt、`orderedTriggerIds` command、validation、合法排序入 `StackItems`、事件日志；该口径已在 4C-1 被保守 APNAP controller-block 子集覆盖。E 只维护矩阵索引、FAQ 证据和阶段 4 优先级，不修改核心规则引擎，不启动最终 18 步 E2E，不进入 1009 张卡 full-official 覆盖。

完成项：

- 在机器矩阵中新增 `stage3DComplexPromptLifecycle` overlay，并为阶段 4 优先 / FAQ / 压测 / 可复用 oracle 候选补 `functionalUnits[].stage3D` 标签。
- 标注复杂 prompt / lifecycle 依赖桶：370 个 `PAY_COST` FUs、287 个 `ASSIGN_COMBAT_DAMAGE` FUs、67 个 `ORDER_TRIGGERS` / battle initial stack 压测 FUs、358 个 battlefield / control / conquer FUs、288 个 spell duel / battle FUs。
- 标注阶段 4 优先级：Top20 high-risk FUs、179 个 FAQ 命中候选、阶段 4 压测卡组 FUs、113 个可复用 oracle / effectId implementation 候选。
- 输出后续适合压测 `ORDER_TRIGGERS` / battle initial stack / trigger ordering 的清单；4C-1 已部分关闭保守 APNAP controller-block 排序，但完整 trigger engine、真实卡牌触发全规则入队、完整 APNAP 多玩家独立排序、trigger cost / decline / payment 仍不关闭。

新增文件：

- `docs/CURRENT_CARD_EFFECT_STAGE3D_ORDER_TRIGGERS_EVIDENCE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`

未修改文件：

- `data/official/card-catalog.zh-CN.json`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `src/**`
- `src/Riftbound.DevUi/**`
- `riftbound-dotnet.sln`

仍存在 P0/P1：

- `ORDER_TRIGGERS` 已升级到 4C-1 保守 APNAP controller-block 子集，但完整 trigger engine、真实卡牌触发全规则入队、完整 APNAP 多玩家独立排序仍未关闭。
- battle initial stack 全规则、attack/defense/conquer/last-breath/standby trigger ordering 全矩阵、trigger cost / decline / payment 仍需后续阶段实现和测试。
- 完整 PaymentEngine、完整 `ASSIGN_COMBAT_DAMAGE` 全规则矩阵、spell duel / battle 全生命周期、LayerEngine、替代/预防、隐藏信息仍未 full-official。
- 1009 snapshot entries / 811 functional units 的 official text、FAQ adjudication、实现、测试闭环仍未完成。

是否允许进入卡牌效果批量覆盖：**不允许。**
## 13. 阶段 4B E 汇总

阶段 4B 名称：卡牌覆盖矩阵冻结。E 只冻结 2026-04-27 官网快照、functional unit、官方文本 / FAQ / rules evidence、测试证据和状态口径；4B 本身不实现卡牌效果，不进入 4C，不修改服务端/前端/A checkpoint/`riftbound-dotnet.sln`。

完成项：

- 在 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4BCardCoverageFreeze` 顶层冻结摘要。
- 为 1009 个 `snapshotEntries[]` 增加 `stage4B`：`collectorId`、`functionalUnitId`、`freezeStatus`、`statusFlags`、`oracleEffectIds`、rules/FAQ refs、automated test status。
- 为 811 个 `functionalUnits[]` 增加 `stage4B`：`freezeStatus`、`statusFlags`、effect implementation、official text hash、rules / FAQ evidence、automated test evidence、full-official blockers。
- 冻结状态枚举：`IMPLEMENTED_TESTED`、`IMPLEMENTED_UNTESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`、`BLOCKED`。
- 明确 token、rune、battlefield、promo、`*` 变体、lowercase suffix / 异画都按官方 snapshot entry 计入 1009；functional unit 复用不减少 card entry 数。

冻结统计：

| 项 | 数量 |
|---|---:|
| snapshot entries | 1009 |
| unique cardIds | 1009 |
| unique collectorIds / cardNo | 1009 |
| functional units | 811 |
| unique oracle/effectIds | 807 |
| token entries | 13 |
| rune entries | 48 |
| battlefield entries | 59 |
| promo `·P` entries | 4 |
| `*` variant entries | 36 |
| lowercase suffix / alternate-art entries | 100 |

Functional unit primary status counts：

| status | FUs |
|---|---:|
| `IMPLEMENTED_TESTED` | 50 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 102 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

Snapshot entry primary status counts：

| status | entries |
|---|---:|
| `IMPLEMENTED_TESTED` | 77 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 273 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

新增文件：

- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`

4B 结论：

- 4B freeze 本身无阻断；矩阵已可解释 1009 snapshot entries -> 811 functional units -> implementation/evidence/tests/status。
- 不授予任何 full-official；`stage4BCardCoverageFreeze.uncoveredSummary.fullOfficialUncoveredFunctionalUnitIds` 仍包含 811/811。
- 4C 批量实现仍需 A 明确授权和写入锁。

是否允许进入卡牌效果批量覆盖：**不允许。**

## 14. 阶段 4C-4 E 汇总

阶段 4C-4 名称：Treasure Pile trigger payment / decline / payment failure 最小真实卡牌切片。E 只更新矩阵与证据边界，不授予 full-official，不进入 1009 张卡批量实现。

完成项：

- 在 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch4TriggerPayment` 顶层 overlay。
- 为 `FU-4694e33f45` / `SFD·220/221`《珍宝堆》增加 `functionalUnits[].stage4C4`。
- 标注服务端 runtime 路径：`TRIGGER_PAYMENT` + `PAY_COST`，合法 choices 为 `SPEND_MANA:1` 与 `DECLINE`。
- 标注负例：wrong player、stale prompt、unknown choice、duplicate choice、pay+decline、malformed payload、insufficient mana 都是 no-mutation 拒绝。
- 明确 4B freezeStatus / statusFlags 不变；`fullOfficial` 仍为 `false`。

新增文件：

- `docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_EVIDENCE.md`
- `docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`

修改文件：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`

仍存在 P0/P1：

- 完整 PaymentEngine 未完成。
- `SFD·220/221` 之外的 triggered-cost functional units 未完成。
- 完整 trigger engine、state-based cleanup trigger enqueue 全族、FAQ adjudication 和正式 18 步 E2E 未完成；4C-5 / 4C-6 后也只覆盖 visible Watchful Sentinel / Honest Broker cleanup 入队代表切片。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖仍为 0。

是否允许批量 full-official 覆盖：**不允许。**

## 37. 阶段 4C-24 E 汇总

阶段 4C-24 名称：Vayne conquer pay-one recall representative baseline。E/A 只更新覆盖矩阵与索引证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·035/298` Vayne / 《薇恩》在冻结矩阵中的真实 FU 为 `FU-c027639a3c`，snapshot entry id / cardId 为 `31243`。
- 当前 oracle/effectId：`OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT`。
- 4B status：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`。

本批记录：

- visible face-up Vayne 征服后开启 `TRIGGER_PAYMENT` / `PAY_COST` pay 1 window。
- 支付 1 后将 Vayne 自身返回 owner hand。
- decline 不召回；invalid source 不开启支付、不移动区域、不泄露状态。
- 只标 `FU-c027639a3c`；Aphelios / `FU-67c6b0186e` 与 Icevale Archer / `FU-c170628e3a` 保留为后续候选，不作为 4C-24 完成项。

4C-24 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C24` verified FUs | 1 |
| `stage4C24` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 3 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

验证：

- B 已完成服务端代码并报告 focused 52/52 通过。
- E 本批只校验覆盖矩阵 JSON / docs diff，不运行或修改服务端、前端、checkpoint、server audit 或 evidence index。

仍存在 P0/P1：

- SFD reprint / promo family full-official 未覆盖。
- Assault3、active-entry、完整 conquer/control-zone movement matrix 未覆盖。
- hidden/random full matrix 与完整 PaymentEngine / PAY_COST full matrix 未覆盖。
- 1009 entries / 811 functional units full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 38. 阶段 4C-25 E 汇总

阶段 4C-25 名称：Icevale Archer attack payment representative baseline。E/A 只更新覆盖矩阵与审计证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `UNL-065/219` Icevale Archer / 《冰谷弓箭手》在冻结矩阵中的真实 FU 为 `FU-c170628e3a`，snapshot entry id / cardId 为 `34598`。
- 当前 oracle/effectId：`ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`。
- 4B status：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`。

本批记录：

- active start-battle task 下 visible face-up Icevale 作为攻击者后开启 `TRIGGER_PAYMENT` / `PAY_COST` pay 1 window。
- 使用 `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标。
- 支付 1 后该目标本回合 power -1；decline 不修改资源或目标战力。
- invalid target、hidden / face-down / standby / opponent-controlled source 不开启支付、不变更状态、不泄漏隐藏信息。
- 只标 `FU-c170628e3a`；Aphelios / `FU-67c6b0186e` 保留为后续 dedicated weapon-attachment three-mode candidate，不作为 4C-25 完成项。

4C-25 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C25` verified FUs | 1 |
| `stage4C25` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 4 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| cumulative attack-payment target-selection verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

验证：

- Focused backend：102/102 通过。
- JSON / diff hygiene：通过。
- Backend full：3429/3429 通过。
- Frontend build：通过。
- Chrome smoke：通过。
- 不得用 focused backend 或 smoke 替代正式 18-step E2E。

仍存在 P0/P1：

- 完整 attack-trigger family、完整 target selection prompt、支付后恢复战斗时点未覆盖。
- complete battle lifecycle、Spellshield target tax、LayerEngine / temporary modifier matrix 未覆盖。
- hidden / face-down 原始触发建模、完整 PaymentEngine / PAY_COST full matrix 未覆盖。

## 39. 阶段 4C-26 E 汇总

阶段 4C-26 名称：Jax weapon-attach pay-one draw-one representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·119/221` / `SFD·119a/221` Jax / 《贾克斯》在冻结矩阵中的真实 FU 为 `FU-73f3be35df`。
- snapshot entries / cardIds：`SFD·119/221` / `33207`，`SFD·119a/221` / `33208`。
- 当前 oracle/effectIds：`SFD_119_JAX_ALT_A_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT`、`SFD_119_JAX_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT`。
- 4B status：`SHARED_ORACLE_IMPLEMENTATION`；statusFlags：`IMPLEMENTED_UNTESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- FAQ refs：`SOUL-JFAQ-260114 p20`、`SOUL-OFAQ-260114 p11` 仍需 FAQ review。

本批记录：

- visible face-up Jax 获得 weapon / armament attachment 后开启既有 `TRIGGER_PAYMENT` / `PAY_COST` pay 1 window。
- `SPEND_MANA:1` 后抽 1；`DECLINE` 不抽牌、不变更状态。
- non-Jax / non-armament 不开启 prompt；hidden / face-down / standby / opponent-controlled source 不泄漏、不开启支付；insufficient payment 不抽牌。
- 只标 `FU-73f3be35df`；Aphelios / `FU-67c6b0186e` 因 mode-choice / mode-memory 契约继续 design-gated，不作为 4C-26 完成项。

4C-26 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C26` verified FUs | 1 |
| `stage4C26` verified snapshot entries | 2 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 5 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| cumulative attack-payment target-selection verified FUs | 1 |
| cumulative weapon-attachment payment-draw verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 weapon / equipment attachment 与 `百炼` / Assemble 规则矩阵未覆盖。
- 完整 PaymentEngine / PAY_COST full matrix、hidden/random-zone draw matrix、hidden / face-down / standby / opponent-controlled source visibility model 未覆盖。
- LayerEngine continuous-effect interactions、FAQ adjudication、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**
- 1009 entries / 811 functional units full-official、FAQ regression、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 40. 阶段 4C-27 E 汇总

阶段 4C-27 名称：Treasure Hunter move-create dormant Gold representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint、server audit、rules evidence index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·130/221` Treasure Hunter / 《寻宝猎人》在冻结矩阵中的真实 FU 为 `FU-6144ab0271`。
- snapshot entry / cardId：`SFD·130/221` / `33220`。
- 当前 oracle/effectId：`TREASURE_HUNTER_MOVE_GOLD_PLAY_UNIT`。
- 4B status：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`CORE-260330 p48`、`SOUL-JFAQ-260114 p21`；FAQ review 仍未关闭。

本批记录：

- visible face-up Treasure Hunter 移动后触发代表路径，创建 / 打出 1 个休眠的 Gold 装备指示物。
- non-Treasure Hunter source 或 non-move event 不创建 Gold。
- hidden / face-down / standby / opponent-controlled source 不触发、不泄漏、不创建 Gold。
- 只标 `FU-6144ab0271`；Karthus / `FU-ee1dfb3ed3` last-breath static extra-trigger route 继续 design-gated / untagged，不作为 4C-27 完成项。

4C-27 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C27` verified FUs | 1 |
| `stage4C27` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 5 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| cumulative attack-payment target-selection verified FUs | 1 |
| cumulative weapon-attachment payment-draw verified FUs | 1 |
| cumulative movement-Gold creation verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 ZoneOwnership / ControlChange / Movement matrix 未覆盖。
- 完整 move-trigger source family、Gold equipment token creation / destination matrix 未覆盖。
- hidden / face-down / standby / opponent-controlled source visibility model 与 FAQ adjudication 未覆盖。
- Karthus `FU-ee1dfb3ed3` last-breath static extra-trigger route、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 41. 阶段 4C-28 E 汇总

阶段 4C-28 名称：Battle or Flight move battlefield unit to owner base representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint、server audit、rules evidence index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·168/298` Battle or Flight / 《战或逃》在冻结矩阵中的真实 FU 为 `FU-813144e7d4`。
- snapshot entry / cardId：`OGN·168/298` / `31398`。
- 当前 oracle/effectId：`BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE`。
- 4B status：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`CORE-260330 p46`、`JFAQ-251023 p4`、`SOUL-JFAQ-260114 p12`、`SOUL-JFAQ-260114 p16`；FAQ review 仍未关闭。

本批记录：

- valid face-up battlefield unit target 被 Battle or Flight 移回其 owner base。
- target guard hardening：non-battlefield、non-unit、hidden、face-down、standby 或其它 invalid target 不移动、不泄漏。
- 只标 `FU-813144e7d4`；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 与 Karthus / `FU-ee1dfb3ed3` 均保留为后续候选，不作为 4C-28 完成项。

4C-28 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C28` verified FUs | 1 |
| `stage4C28` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 5 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| cumulative attack-payment target-selection verified FUs | 1 |
| cumulative weapon-attachment payment-draw verified FUs | 1 |
| cumulative movement-Gold creation verified FUs | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 spell-duel / battle lifecycle 与 FEPR target legality matrix 未覆盖。
- 完整 ZoneOwnership / ControlChange / Movement matrix、hidden / face-down / standby target visibility model 未覆盖。
- PaymentEngine / PAY_COST full matrix、FAQ adjudication、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 42. 阶段 4C-29 E 汇总

阶段 4C-29 名称：Gust return power-three-or-less battlefield unit to owner hand representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint、server audit、rules evidence index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·169/298` Gust / 《罡风》在冻结矩阵中的真实 FU 为 `FU-48662b7661`。
- snapshot entry / cardId：`OGN·169/298` / `31399`。
- 当前 oracle/effectId：`GUST_RETURN_BATTLEFIELD_UNIT_POWER_3_OR_LESS_TO_HAND`。
- 4B status：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules / FAQ refs：无；FAQ status 为 `NO_FAQ_CANDIDATE_IN_MATRIX`。

本批记录：

- valid public battlefield unit 且 power <= 3 的目标被返回 owner hand。
- service-authoritative target guard：power > 3、non-battlefield / base unit、stale object、face-down standby、battlefield equipment 均不返回、不泄漏。
- focused tests 已由 A 报告通过：Gust / Return / Hand filter 112/112，Gust / BattleOrFlight filter 13/13。
- full backend 3458/3458、frontend build 和 Chrome smoke 均 passed；不得用 focused tests 替代正式 18-step E2E。
- 只标 `FU-48662b7661`；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c`、Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 均保留为后续候选或 design-gated，不作为 4C-29 完成项。

4C-29 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C29` verified FUs | 1 |
| `stage4C29` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 5 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| cumulative attack-payment target-selection verified FUs | 1 |
| cumulative weapon-attachment payment-draw verified FUs | 1 |
| cumulative movement-Gold creation verified FUs | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 1 |
| cumulative return-to-owner-hand target-guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 FEPR target selection / target legality matrix 未覆盖。
- 完整 ZoneOwnership / ControlChange / Movement matrix、hidden / face-down / standby target visibility model 未覆盖。
- all ReturnsTargetToHand cards、full Gust official completion、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 43. 阶段 4C-30 E 汇总

阶段 4C-30 名称：Hunt the Weak destroy power-three-or-less public battlefield unit guard representative baseline。E/B 只更新覆盖矩阵与风险证据，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint、server audit、rules evidence index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `UNL-159/219` Hunt the Weak / 《狩魂》在冻结矩阵中的真实 FU 为 `FU-282b6e3149`。
- snapshot entry / cardId：`UNL-159/219` / `34707`。
- 当前 oracle/effectId：`HUNT_THE_WEAK_DESTROY_BATTLEFIELD_UNIT_POWER_3_OR_LESS`。
- 4B status：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules / FAQ refs：无；FAQ status 为 `NO_FAQ_CANDIDATE_IN_MATRIX`。

本批记录：

- valid public battlefield unit 且 power <= 3 的目标被摧毁。
- service-authoritative target guard：power > 3、non-battlefield / base unit、stale object、face-down standby、battlefield equipment 均不摧毁、不泄漏。
- focused tests 已由 B 报告通过：Hunt the Weak focused guard 34/34，adjacent guard 19/19。
- full backend 3464/3464、frontend build passed、Chrome smoke passed；不得用 focused tests / smoke 替代正式 18-step E2E。
- 只标 `FU-282b6e3149`；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c`、Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 均保留为后续候选或 design-gated，不作为 4C-30 完成项。

4C-30 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C30` verified FUs | 1 |
| `stage4C30` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 5 |
| cumulative spell-played immediate trigger-event verified FUs | 1 |
| cumulative conquer-payment recall verified FUs | 1 |
| cumulative attack-payment target-selection verified FUs | 1 |
| cumulative weapon-attachment payment-draw verified FUs | 1 |
| cumulative movement-Gold creation verified FUs | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 1 |
| cumulative return-to-owner-hand target-guard verified FUs | 1 |
| cumulative destroy-target guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 FEPR target selection / target legality matrix 未覆盖。
- 完整 replacement / prevention / cleanup after destruction matrix 未覆盖。
- 完整 hidden / face-down / standby target visibility model 未覆盖。
- all destroy-target card family coverage、full Hunt the Weak official completion、1009/811 full-official、正式 18-step E2E 仍未完成。
- full backend 3464/3464、frontend build、Chrome smoke 已通过，但仍不得替代正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**

## 44. 阶段 4C-31 E 汇总

阶段 4C-31 名称：Reprimand return-to-owner-hand target-guard representative baseline。E/B 只更新覆盖矩阵与风险证据 prose，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint、server audit、rules evidence index、matrix JSON 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·172/298` Reprimand / 《责退》在冻结矩阵中的真实 FU 为 `FU-d0383ed260`。
- snapshot entry / cardId：`OGN·172/298` / `31402`。
- 当前 oracle/effectId：`REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND`。
- 4B status 保守不变：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules / FAQ refs：无；FAQ status 为 `NO_FAQ_CANDIDATE_IN_MATRIX`。

本批记录：

- valid public battlefield unit target -> service-authoritative guard -> return target to owner hand。
- invalid target guards：base unit、stale object、face-down standby、battlefield equipment、battlefield spell object、battlefield rune object 均 no mutation / no leak。
- 验证结果：focused 58/58、adjacent guard 24/24、backend full 3471/3471、frontend build passed、Chrome smoke passed。
- 只记录 `FU-d0383ed260` 的代表切片；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-31 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C31` verified FUs | 1 |
| `stage4C31` verified snapshot entries | 1 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 FEPR targeting / target legality matrix 未覆盖。
- 完整 movement / control-zone matrix 未覆盖。
- 完整 hidden visibility / face-down / standby visibility model 未覆盖。
- all return-to-hand family、FAQ adjudication、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 45. 阶段 4C-32 E 汇总

阶段 4C-32 名称：Ride the Wind move-friendly-battlefield-unit-to-owner-base ready target-guard representative baseline。E/B 只更新覆盖矩阵与风险证据，不触碰 `docs/CURRENT_A_MASTER_CHECKPOINT.md`、服务端、前端、checkpoint、server audit、rules evidence index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·173/298` Ride the Wind / 《驭风而行》在冻结矩阵中的真实 FU 为 `FU-6f84196631`。
- snapshot entry / cardId：`OGN·173/298` / `31403`。
- 当前 oracle/effectId：`RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`JFAQ-251023 p4`，仍需 FAQ adjudication。

本批记录：

- friendly public battlefield unit target -> service-authoritative guard -> ready target -> move target to owner base。
- invalid target guards：enemy battlefield unit、friendly base unit、stale unit、face-down standby、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object 均 no mutation / no leak。
- 只记录 `FU-6f84196631` / `OGN·173/298` 的代表切片；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-32 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C32` verified FUs | 1 |
| `stage4C32` verified snapshot entries | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 2 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 movement / roam / precise battlefield model 未覆盖。
- 完整 FEPR targeting / target legality matrix 未覆盖。
- 完整 hidden visibility / face-down / standby visibility model 未覆盖。
- `JFAQ-251023 p4` FAQ adjudication、all movement spell family、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 46. 阶段 4C-33 E 汇总

阶段 4C-33 名称：Charm move-enemy-battlefield-unit-to-owner-base target-guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·043/298` Charm / 《魅惑妖术》在冻结矩阵中的真实 FU 为 `FU-1586b6cdd9`。
- snapshot entry / cardId：`OGN·043/298` / `31255`。
- 当前 oracle/effectId：`CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE`。
- 4B status 保守不变：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules / FAQ refs：无；FAQ status 为 `NO_FAQ_CANDIDATE_IN_MATRIX`。

本批记录：

- enemy public battlefield unit target -> service-authoritative guard -> move target to owner base。
- invalid target guards：friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 no mutation / no leak。
- 验证结果：focused 35/35、adjacent guard 40/40、backend full 3487/3487、frontend build passed、Chrome smoke passed。
- 只记录 `FU-1586b6cdd9` / `OGN·043/298` 的代表切片；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-33 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C33` verified FUs | 1 |
| `stage4C33` verified snapshot entries | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 3 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 movement / roam / precise battlefield model 未覆盖。
- 完整 FEPR targeting / target legality matrix 未覆盖。
- 完整 hidden visibility / face-down / standby visibility model 未覆盖。
- all movement spell family、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 47. 阶段 4C-34 E 汇总

阶段 4C-34 名称：Isolate move-enemy-battlefield-unit-to-owner-base no-draw target-guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `UNL-124/219` Isolate / 《隔绝》在冻结矩阵中的真实 FU 为 `FU-175d573ae4`。
- snapshot entry / cardId：`UNL-124/219` / `34667`。
- 当前 oracle/effectId：`ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW`。
- 4B status 保守不变：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules / FAQ refs：无；FAQ status 为 `NO_FAQ_CANDIDATE_IN_MATRIX`。

本批记录：

- enemy public battlefield unit target -> service-authoritative guard -> move target to owner base -> no card draw。
- invalid target guards：friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 no mutation / no draw / no leak。
- 验证结果：focused 46/46、adjacent guard 48/48、backend full 3495/3495、frontend build passed、Chrome smoke passed。
- 只记录 `FU-175d573ae4` / `UNL-124/219` 的代表切片；Vengeance / `FU-07104fa58a` 可作为下一低耦合 destroy-target 候选；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-34 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C34` verified FUs | 1 |
| `stage4C34` verified snapshot entries | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 4 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- Isolate 落单敌方单位抽牌分支未覆盖。
- 完整 movement / roam / precise battlefield model 未覆盖。
- 完整 FEPR targeting / target legality matrix 未覆盖。
- 完整 hidden visibility / face-down / standby visibility model 未覆盖。
- all movement spell family、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 48. 阶段 4C-35 E 汇总

阶段 4C-35 名称：Vengeance public-unit destroy target-guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·229/298` Vengeance / 《复仇》在冻结矩阵中的真实 FU 为 `FU-07104fa58a`。
- snapshot entry / cardId：`OGN·229/298` / `31467`。
- 当前 oracle/effectId：`VENGEANCE_DESTROY_UNIT`。
- 4B status 保守不变：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules / FAQ refs：无；FAQ status 为 `NO_FAQ_CANDIDATE_IN_MATRIX`。

本批记录：

- public unit target -> service-authoritative guard -> destroy target to owner graveyard。
- valid target guards：friendly / enemy public unit targets in base / battlefield 均可被摧毁。
- invalid target guards：stale unit、face-down standby、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit 均 no mutation / no destroy / no leak。
- 验证结果：focused 107/107、adjacent guard 23/23、backend full 3506/3506、frontend build passed、Chrome smoke passed。
- 只记录 `FU-07104fa58a` / `OGN·229/298` 的代表切片；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-35 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C35` verified FUs | 1 |
| `stage4C35` verified snapshot entries | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 4 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| cumulative destroy-target guard verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 destroy / cleanup / replacement / prevention / Last Breath interaction 未覆盖。
- 完整 FEPR targeting / target legality matrix 未覆盖。
- 完整 hidden visibility / face-down / standby visibility model 未覆盖。
- Spellshield target tax、target invalidation、attached-equipment detach / replacement breadth、destroyed-this-turn memory 未覆盖。
- all destroy-target spell family、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**


## 49. 阶段 4C-36 E 汇总

阶段 4C-36 名称：Hostile Takeover gain-control ready enemy battlefield unit target-guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·202/221` Hostile Takeover / 《恶意收购》在冻结矩阵中的真实 FU 为 `FU-00ee09c2cc`。
- snapshot entry / cardId：`SFD·202/221` / `33301`。
- 当前 oracle/effectId：`HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT`。
- 4B status 保守不变：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`CORE-260330 p46`、`SOUL-JFAQ-260114 p22`、`SOUL-OFAQ-260114 p21`；FAQ refs remain open。

本批记录：

- enemy public battlefield unit target -> service-authoritative guard -> gain control -> ready target。
- invalid target guards：friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 no mutation / no control change / no leak。
- 验证结果：focused 265/265、adjacent guard 157/157、backend full 3515/3515、frontend build passed、Chrome smoke passed。
- 只记录 `FU-00ee09c2cc` / `SFD·202/221` 的代表切片；Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-36 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C36` verified FUs | 1 |
| `stage4C36` verified snapshot entries | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 4 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| cumulative destroy-target guard verified FUs | 2 |
| cumulative control-ready target guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 control-change matrix 未覆盖。
- 完整 ready / exhaust semantics 未覆盖。
- 完整 FEPR targeting / target legality matrix 未覆盖。
- payment / battle / hidden-zone interactions 未覆盖。
- FAQ adjudication for `SOUL-JFAQ-260114 p22` / `SOUL-OFAQ-260114 p21` 未完成。
- all gain-control cards、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**


## 50. 阶段 4C-37 E 汇总

阶段 4C-37 名称：Berserk Impulse opponent top main-deck unit target-guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·025/298` Berserk Impulse / 《暴怒冲动》在冻结矩阵中的真实 FU 为 `FU-b05eda44ce`。
- snapshot entry / cardId：`OGN·025/298` / `31231`。
- 当前 oracle/effectId：`BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ ref：`SOUL-OFAQ-260114 p4`；FAQ ref remains open。

本批记录：

- valid opponent top main-deck unit -> service-authoritative guard -> representative play-opponent-top-unit route。
- invalid guards：opponent top non-unit、non-top object、friendly deck object、private / unauthorized object、face-down / hidden invalid object、dirty resolution / stale top object 均 no play / no mutation / no leak。
- 验证结果：focused 17/17 passed；backend full 3529/3529 passed; frontend build passed; Chrome smoke passed。
- 只记录 `FU-b05eda44ce` / `OGN·025/298` 的代表切片；Edge of Night / `FU-804412488c` 仍为高风险后续候选，Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-37 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C37` verified FUs | 1 |
| `stage4C37` verified snapshot entries | 1 |
| cumulative targeted movement-to-owner-base verified FUs | 4 |
| cumulative return-to-owner-hand target-guard verified FUs | 2 |
| cumulative destroy-target guard verified FUs | 2 |
| cumulative control-ready target guard verified FUs | 1 |
| cumulative opponent top-unit play guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- 完整 opponent-deck reveal / choice / recycle semantics 未覆盖。
- 完整 hidden-zone visibility matrix 未覆盖。
- 完整 FEPR timing / target selection matrix 未覆盖。
- payment / battle / control-zone interactions 未覆盖。
- FAQ adjudication for `SOUL-OFAQ-260114 p4` 未完成。
- all opponent-deck play effects、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 51. 阶段 4C-38 E 汇总

阶段 4C-38 名称：Edge of Night play-equipment / assemble-purple target guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·139/221` Edge of Night / 《夜之锋刃》在冻结矩阵中的真实 FU 为 `FU-804412488c`。
- snapshot entry / cardId：`SFD·139/221` / `33229`。
- 当前 oracle/effectId：`EDGE_OF_NIGHT_PLAY_EQUIPMENT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ ref：`SOUL-OFAQ-260114 p10`、`SOUL-OFAQ-260114 p9`；FAQ refs remain open。

本批记录：

- ordinary play-equipment hand route：0 target -> stack / pass-pass -> base equipment。
- explicit play target rejected without payment or mutation。
- valid base Edge of Night `ASSEMBLE_PURPLE` route：friendly public unit target -> pay 1 purple -> `COST_PAID` + `EQUIPMENT_ATTACHED`。
- invalid assemble guards：face-down / hidden source、source in hand、opponent source、already-attached source、unknown source、unknown / opponent / face-down standby / non-unit target、missing / wrong optional cost、insufficient purple 均 no tick / no events / no payment / no stack / no attach / no leak。
- 验证结果：focused 98/98 passed；backend full 3546/3546 passed; frontend build passed; Chrome smoke passed。
- 只记录 `FU-804412488c` / `SFD·139/221` 的代表切片；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-38 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C38` verified FUs | 1 |
| `stage4C38` verified snapshot entries | 1 |
| cumulative equipment assemble guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- Edge of Night full standby immediate attach 未覆盖。
- complete hidden-zone prompt / redaction / visibility policy 未覆盖。
- complete equipment layer / attach / detach / replacement matrix 未覆盖。
- FAQ adjudication for `SOUL-OFAQ-260114 p10` / `SOUL-OFAQ-260114 p9` 未完成。
- 1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 52. 阶段 4C-39 E 汇总

阶段 4C-39 名称：Zhonyas Hourglass play-equipment guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·077/298` Zhonya's Hourglass / 《中娅沙漏》在冻结矩阵中的真实 FU 为 `FU-fb79eea7fc`。
- snapshot entry / cardId：`OGN·077/298` / `31291`。
- 当前 oracle/effectId：`ZHONYAS_HOURGLASS_PLAY_EQUIPMENT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`SOUL-JFAQ-260114 p15`、`SOUL-JFAQ-260114 p2`、`SOUL-JFAQ-260114 p23`、`SOUL-JFAQ-260114 p9`、`SOUL-OFAQ-260114 p8` remain open。

本批记录：

- ultra-narrow representative play-equipment guard route for `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT`。
- invalid source / target / cost guards reject without mutation or leak。
- 验证结果：focused 268/268 passed；backend full 3552/3552 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-fb79eea7fc` / `OGN·077/298` 的代表切片；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-39 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C39` verified FUs | 1 |
| `stage4C39` verified snapshot entries | 1 |
| cumulative equipment play guard verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- full standby / reaction semantics 未覆盖。
- destroy replacement matrix 未覆盖。
- recall interactions 未覆盖。
- complete equipment layer / continuous-effect matrix 未覆盖。
- FAQ adjudication for Zhonya refs 未完成。
- all equipment cards、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 53. 阶段 4C-40 E 汇总

阶段 4C-40 名称：Sea Monster Hook play-equipment guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGN·242/298` Sea Monster Hook / 《海兽钓钩》在冻结矩阵中的真实 FU 为 `FU-2653af0380`。
- snapshot entry / cardId：`OGN·242/298` / `31482`。
- 当前 oracle/effectId：`SEA_MONSTER_HOOK_PLAY_EQUIPMENT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`BREAK-JFAQ-260416 p9`、`JFAQ-251023 p2`、`JFAQ-251023 p3`、`SOUL-JFAQ-260114 p22` remain open。

本批记录：

- ultra-narrow representative play-equipment guard route for `SEA_MONSTER_HOOK_PLAY_EQUIPMENT`。
- invalid source / target / cost guards reject without mutation or leak。
- 验证结果：focused 272/272 passed；backend full 3558/3558 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-2653af0380` / `OGN·242/298` 的代表切片；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-40 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C40` verified FUs | 1 |
| `stage4C40` verified snapshot entries | 1 |
| cumulative equipment play guard verified FUs | 3 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- activated ability semantics 未覆盖。
- top-five search / reveal / choice semantics 未覆盖。
- free play route 未覆盖。
- recycle route 未覆盖。
- complete equipment layer / continuous-effect matrix 未覆盖。
- FAQ adjudication for Sea Monster Hook refs 未完成。
- all equipment cards、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 54. 阶段 4C-41 E 汇总

阶段 4C-41 名称：Giant Arm Kato play-keyword-unit guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·112/221` Giant Arm Kato / 《巨腕加藤》在冻结矩阵中的真实 FU 为 `FU-464ec8c275`。
- snapshot entry / cardId：`SFD·112/221` / `33198`。
- 当前 oracle/effectId：`GIANT_ARM_KATO_PLAY_KEYWORD_UNIT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`SOUL-JFAQ-260114 p12`、`SOUL-JFAQ-260114 p3`、`SOUL-OFAQ-260114 p12` remain open。

本批记录：

- ultra-narrow representative play-keyword-unit guard route for `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT`。
- invalid source / target / cost guards reject without mutation or leak。
- 验证结果：focused 99/99 passed；backend full 3564/3564 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-464ec8c275` / `SFD·112/221` 的代表切片；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-41 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C41` verified FUs | 1 |
| `stage4C41` verified snapshot entries | 1 |
| cumulative play-keyword-unit guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- Spellshield target tax 未覆盖。
- move trigger 未覆盖。
- keyword grant semantics 未覆盖。
- +power until EOT semantics 未覆盖。
- complete LayerEngine / continuous-effect matrix 未覆盖。
- FAQ adjudication for Giant Arm Kato refs 未完成。
- all keyword-unit cards、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 55. 阶段 4C-42 E 汇总

阶段 4C-42 名称：Time Gate play-equipment guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·078/221` Time Gate / 《预时之门》在冻结矩阵中的真实 FU 为 `FU-081d97eb3e`。
- snapshot entry / cardId：`SFD·078/221` / `33158`。
- 当前 oracle/effectId：`TIME_GATE_PLAY_EQUIPMENT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`BREAK-JFAQ-260416 p11`、`SOUL-JFAQ-260114 p15`、`SOUL-JFAQ-260114 p19`、`SOUL-JFAQ-260114 p25`、`SOUL-JFAQ-260114 p6`、`SOUL-OFAQ-260114 p21` remain open。

本批记录：

- ultra-narrow representative play-equipment guard route for `TIME_GATE_PLAY_EQUIPMENT`。
- invalid source / target / cost guards reject without mutation or leak。
- 验证结果：focused 292/292 passed；backend full 3570/3570 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-081d97eb3e` / `SFD·078/221` 的代表切片；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 仍 design-gated。

4C-42 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C42` verified FUs | 1 |
| `stage4C42` verified snapshot entries | 1 |
| cumulative equipment play guard verified FUs | 4 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- activated / tap ability 未覆盖。
- next spell Echo semantics 未覆盖。
- optional echo payment / repeat route 未覆盖。
- duration cleanup 未覆盖。
- FAQ adjudication for Time Gate refs 未完成。
- all equipment cards、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 56. 阶段 4C-43 E 汇总

阶段 4C-43 名称：Sfur Song play-equipment guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·059/221` Sfur Song / 《斯弗尔尚歌》在冻结矩阵中的真实 FU 为 `FU-9a623b3185`。
- snapshot entry / cardId：`SFD·059/221` / `33139`。
- 当前 oracle/effectId：`SFUR_SONG_PLAY_EQUIPMENT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`SOUL-JFAQ-260114 p24`、`SOUL-JFAQ-260114 p25`、`SOUL-JFAQ-260114 p8`、`SOUL-OFAQ-260114 p18`、`SOUL-OFAQ-260114 p19` remain open。

本批记录：

- ultra-narrow representative play-equipment guard route for `SFUR_SONG_PLAY_EQUIPMENT`。
- invalid source / target / cost guards reject without mutation or leak。
- 验证结果：focused 268/268 passed；backend full 3576/3576 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-9a623b3185` / `SFD·059/221` 的代表切片；Akshan / `FU-7419ee7d9d`、Switcheroo / `FU-0b6332bbf0` 仍待后续；Void Burrower / `FU-6e7d0dba2c` 与 Sett / `FU-6308c2db01` 仍适合 legend-domain/shared-oracle batch。

4C-43 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C43` verified FUs | 1 |
| `stage4C43` verified snapshot entries | 1 |
| cumulative equipment play guard verified FUs | 5 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- copied host skill text 未覆盖。
- continuous text / LayerEngine semantics 未覆盖。
- complete assemble / equipment attach lifecycle 未覆盖。
- equipment control / zone movement 未覆盖。
- FAQ full behavior for Sfur Song refs 未完成。
- all equipment cards、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 57. 阶段 4C-44 E 汇总

阶段 4C-44 名称：Akshan play-unit guard representative baseline。E/A 只更新覆盖矩阵与风险证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·109/221` Akshan / 《阿克尚》在冻结矩阵中的真实 FU 为 `FU-7419ee7d9d`。
- snapshot entry / cardId：`SFD·109/221` / `33194`。
- 当前 oracle/effectId：`AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`。
- 4B status 保守不变：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`SOUL-JFAQ-260114 p18`、`SOUL-JFAQ-260114 p23` remain open。

本批记录：

- ultra-narrow representative play-unit guard route for `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`。
- invalid source / target / cost guards reject without mutation or leak。
- 验证结果：focused 189/189 passed；backend full 3582/3582 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-7419ee7d9d` / `SFD·109/221` 的代表切片；Switcheroo / `FU-0b6332bbf0` 仍待后续；Void Burrower / `FU-6e7d0dba2c` 与 Sett / `FU-6308c2db01` 仍适合 legend-domain/shared-oracle batch。

4C-44 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C44` verified FUs | 1 |
| `stage4C44` verified snapshot entries | 1 |
| cumulative play-unit guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- optional assemble 未覆盖。
- extra play semantics 未覆盖。
- cleanup / replacement / duration matrix 未覆盖。
- movement / control-zone matrix 未覆盖。
- payment / cost windows 未覆盖。
- targeting / stack timing 未覆盖。
- LayerEngine / continuous effects 未覆盖。
- FAQ adjudication for Akshan refs 未完成。
- all Akshan official text、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 58. 阶段 4C-45 E 汇总

阶段 4C-45 名称：Switcheroo battlefield power-swap guard representative baseline。E 只更新覆盖矩阵与 coverage/risk/freeze 文档，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·145/221` Switcheroo / 《换换乐》在冻结矩阵中的真实 FU 为 `FU-0b6332bbf0`。
- snapshot entry / cardId：`SFD·145/221` / `33237`。
- 当前 oracle/effectId：`SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS`。
- 4B status 保守不变：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`SOUL-JFAQ-260114 p14` remains open。

本批记录：

- ultra-narrow representative battlefield power-swap guard route for `SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS`。
- invalid source / target / timing guards reject without mutation or leak。
- 验证结果：focused 284/284 passed；backend full 3594/3594 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-0b6332bbf0` / `SFD·145/221` 的代表切片；Void Burrower / `FU-6e7d0dba2c` 与 Sett / `FU-6308c2db01` 仍适合 legend-domain/shared-oracle batch。

4C-45 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C45` verified FUs | 1 |
| `stage4C45` verified snapshot entries | 1 |
| cumulative battlefield power-swap guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- full battle math 未覆盖。
- LayerEngine / continuous effect semantics 未覆盖。
- cleanup / replacement / duration matrix 未覆盖。
- hidden / random-zone behavior 未覆盖。
- payment / timing matrix 未覆盖。
- FAQ adjudication for Switcheroo refs 未完成。
- all Switcheroo official text、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 59. 阶段 4C-46 E 汇总

阶段 4C-46 名称：Void Burrower legend-domain shared-oracle design gate。E 只更新覆盖矩阵与 coverage/risk/freeze 文档，不触碰 `riftbound-dotnet.sln`，不进入实现或 1009 张卡 full-official。

身份核对：

- `SFD·187/221` Void Burrower / 《虚空遁地兽》在冻结矩阵中的真实 FU 为 `FU-6e7d0dba2c`。
- snapshot entries / cardIds：`SFD·187/221` / `33285`，`SFD·243/221` / `33354`。
- 当前 oracle/effectId：`LEGEND_ACTION_DOMAIN`。
- 4B status 保守不变：`SHARED_ORACLE_IMPLEMENTATION`；statusFlags：`IMPLEMENTED_UNTESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`SOUL-JFAQ-260114 p14`、`SOUL-OFAQ-260114 p4` remain open。

本批记录：

- NO-GO direct implementation for `LEGEND_ACTION_DOMAIN` shared-oracle Void Burrower。
- required design gates：`LegendActivePredicate`、`LegendOptionalTrigger`、`RevealChoice`、`ReplacementPayment`、shared-oracle mapping、hidden/reveal redaction。
- 本批不记录 focused implementation evidence；verified implementation counters stay 0。A checkpoint validation confirmed the docs/matrix overlay did not break baseline: backend full 3594/3594 passed, frontend build passed, Chrome smoke passed。
- Sett / `FU-6308c2db01` / representative `OGN·269/298` / cardId `31512` 保留为 follow-on design candidate；本批不打 `stage4C46`。

4C-46 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C46` design-gated FUs | 1 |
| `stage4C46` design-gated snapshot entries | 2 |
| `stage4C46` verified FUs | 0 |
| `stage4C46` verified snapshot entries | 0 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- LegendActivePredicate 未设计完成。
- LegendOptionalTrigger 未设计完成。
- RevealChoice 未设计完成。
- ReplacementPayment 未设计完成。
- shared-oracle mapping 未设计完成。
- hidden / reveal redaction 未设计完成。
- full legend action domain、FAQ adjudication for Void Burrower refs、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 60. 阶段 4C-47 E 汇总

阶段 4C-47 名称：Draven battle body / play-unit guard representative baseline。E 只更新覆盖矩阵与 coverage/risk/freeze 文档，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `SFD·020/221` Draven / 《德莱文》在冻结矩阵中的真实 FU 为 `FU-964b214448`。
- snapshot entries / cardIds：`SFD·020/221` / `33092`，`SFD·020a/221` / `33093`。
- 当前 oracle/effectId：`SFD_020_DRAVEN_VANILLA_PLAY_UNIT`、`SFD_020A_DRAVEN_VANILLA_PLAY_UNIT`。
- 4B status 保守不变：`IMPLEMENTED_TESTED`；statusFlags：`IMPLEMENTED_TESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- rules / FAQ refs：`BREAK-JFAQ-260416 p4`、`SOUL-JFAQ-260114 p25`、`SOUL-JFAQ-260114 p4`、`SOUL-OFAQ-260114 p16`、`SOUL-OFAQ-260114 p17` remain open。

本批记录：

- ultra-narrow representative Draven battle body / play-unit guard route for the shared-oracle FU。
- invalid source / target / timing guards reject without mutation or leak。
- 验证结果：focused 14/14 passed；backend full 3601/3601 passed；frontend build passed；Chrome smoke passed。
- 只记录 `FU-964b214448` / `SFD·020/221` + `SFD·020a/221` 的代表切片；Vex / `FU-9f7cb73dc4` 可作为后续 lower-coupling single-entry candidate；Sett / `FU-6308c2db01` 仍适合 legend-domain/shared-oracle design batch。

4C-47 矩阵 overlay 统计口径：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C47` verified FUs | 1 |
| `stage4C47` verified snapshot entries | 2 |
| cumulative battle-body guard verified FUs | 1 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

仍存在 P0/P1：

- battle win Gold 未覆盖。
- attack / defense red payment 未覆盖。
- +2 until EOT 未覆盖。
- PaymentEngine 未完整官方化。
- Layer / duration 未完整官方化。
- battle lifecycle full matrix 未完成。
- FAQ adjudication for Draven refs 未完成。
- all Draven official text、1009/811 full-official、正式 18-step E2E 仍未完成。

是否允许批量 full-official 覆盖：**不允许。**

## 36. 阶段 4C-23 E 汇总

阶段 4C-23 名称：Lux high-cost spell temporary power representative baseline。E/A 只更新覆盖矩阵与索引证据，不触碰 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

身份核对：

- `OGS·006/024` Lux / 《拉克丝》在冻结矩阵中的真实 FU 为 `FU-f18a49e06d`，snapshot entry id 为 `31585`。
- 当前 oracle/effectId：`OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT`；runtime 代表效果 kind 为 `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`。
- 4B status：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。

本批记录：

- visible face-up Lux 由其 controller 打出 cost >= 5 spell 后记录 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events。
- Lux 获得 `POWER_MODIFIED_UNTIL_END_OF_TURN` +3，power 5 -> 8，`UntilEndOfTurnPowerModifier` 0 -> 3。
- low-cost spell、opponent spell、face-down / standby / invalid source no trigger / no mutation。
- 只标 `FU-f18a49e06d`；Aphelios / `FU-67c6b0186e` 保留为 dedicated weapon-attachment three-mode candidate，不作为 4C-23 完成项。

4C-23 矩阵 overlay 统计：

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C23` verified FUs | 1 |
| `stage4C23` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 16 |
| cumulative state-based cleanup trigger enqueue verified FUs | 14 |
| cumulative hand-choice prompt verified FUs | 1 |
| cumulative trigger-payment verified FUs | 2 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

验证：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCostSpell|FullyQualifiedName~Ravenbloom|FullyQualifiedName~RealTriggerQueue"` 通过 67/67。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3413/3413。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。

仍存在 P0/P1：

- 完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- 完整 PaymentEngine、paid-cost override、增减费、额外费用、替代费用 full matrix。
- LayerEngine、timestamp/dependency、temporary modifier cleanup duration matrix。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**
