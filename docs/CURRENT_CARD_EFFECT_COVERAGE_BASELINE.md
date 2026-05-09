# 当前卡牌效果覆盖基线

更新时间：2026-05-09

阶段：**阶段 3B / E 卡牌覆盖矩阵与 Battlefield lifecycle 证据 overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文只建立统计口径、只读数据基线、矩阵字段、风险排序和阶段性证据 overlay，不实现或修改任何卡牌效果。阶段 1/2 建立卡牌覆盖基线；阶段 3A/3B 只给最小 runtime / lifecycle 切片补证据标签，防止把代表路径、旧阶段口径或图鉴展示误判为全官方卡牌完成。

## 1. 已读取依据

- `docs/A_MASTER_AGENT_GOAL.md`：最终目标要求 1009 张卡完整映射，但阶段 4 才进入卡牌效果覆盖。
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`：当前主控结论为 `NOT READY`；E 的职责是卡牌效果覆盖、官方文本与 FAQ 证据矩阵。
- `docs/符文战场_服务端核心规则自查文档.md`：单卡审计必须建立“文本 - 结构化效果 - 测试”三方一致性，并覆盖 FAQ。
- `docs/rules-evidence-index.md`：当前已有规则域和 fixture 证据目录；该文件不是 1009 张卡覆盖矩阵。
- `data/official/card-catalog.zh-CN.json`：固定官网快照，`fetchedAt = 2026-04-27`，声明 `total = 1009`。

注意：`docs/CURRENT_RULE_EVIDENCE_TODO.md` 可能由 D/A 或其他 worker 持有；本轮 3B E 不修改、不追加。

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
- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 等复杂 prompt / command / payload schema 仍未正式完成。
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
