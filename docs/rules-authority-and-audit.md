# 规则权威与重审协议

更新时间：2026-04-28

## 1. 结论

从 2026-04-28 起，新项目的底层规则权威由五份官方 PDF 与官网卡牌快照共同构成。旧 Java 项目没有纳入四份 FAQ，因此不能再作为最终规则裁决源，只能作为历史实现参考、fixture 导出工具和回归对照样本。

任何已经开发完成的能力，如果只对齐了旧 Java 行为但没有核对五份 PDF，状态必须降级为 `NEEDS_RULE_AUDIT`，通过重审后才能标记为完成。

## 2. 当前官方 PDF 清单

这些 PDF 只作为本地规则资料，不提交到 Git。

| 文件 | 页数 | 作用 |
|---|---:|---|
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》核心规则_260330.pdf` | 105 | 核心规则主干。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/裁判FAQ_251023.pdf` | 10 | 官方裁判 FAQ，补充和澄清特定场景。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_裁判FAQ.pdf` | 25 | `铸魂淬炼` 系列裁判 FAQ。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_官方FAQ_260114.pdf` | 21 | `铸魂淬炼` 系列官方 FAQ。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》破限系列_裁判FAQ_260416.pdf` | 11 | `破限` 系列裁判 FAQ。 |

## 3. 裁决优先级

1. 官网卡牌原文与核心规则中的黄金法则。
2. 五份官方 PDF 规则资料，其中 FAQ 对核心规则中不准确、不完整或未覆盖的具体场景具有澄清权。
3. 同属官方资料但存在冲突时，优先采用更具体、更新、且明确针对该场景的 FAQ 条目。
4. 官网卡牌快照 `data/official/card-catalog.zh-CN.json`，用于卡面文本、类型、费用、数值、关键词和素材索引。
5. 旧 Java 实现、测试、fixture 和生成矩阵，仅作历史参考；若与五份 PDF 或官网卡面冲突，必须修改新项目规则设计，不能为了 conformance 迁就 Java。

## 4. Java Oracle 的新定位

`java-oracle` fixture 仍然有价值，但语义从“最终规则金标准”调整为“旧实现行为样本”。

使用规则：

- 可继续用 Java exporter 生成 command log、events、snapshots，帮助新引擎保持迁移可控。
- 每条 fixture 必须补充 `rulesEvidence` 或等价文档记录，指向核心规则 PDF、FAQ 和官网卡面依据。
- 如果 Java 与 FAQ 不一致，新增 `expected` 应以 PDF/FAQ 裁决为准，同时保留 Java 输出作为 `legacyOracle` 对照。
- 已对齐 Java 的测试不能单独作为完成门禁，只能作为回归门禁之一。

## 5. 已开发部分重审清单

当前新项目仍处于骨架期，但以下已完成内容必须重新审查：

| 模块 | 当前状态 | 重审要求 |
|---|---|---|
| `MatchSession` 串行与幂等 | 工程行为可保留 | 核对 FAQ 中是否有重复提交、让过、响应窗口相关裁决；若无冲突，保持现状。 |
| `PASS` fixture | 已对齐 Java | 核对核心规则与 FAQ 中让过、连续让过、普通开环/闭环、结算链和法术对决规则。 |
| `END_TURN` fixture | 已对齐 Java | 核对回合结束、回合开始、通道符文、抽牌、临时效果清理、控制权归还与 FAQ 特例。 |
| 玩家视角 snapshot | 只有骨架 | 必须核对公开、私密、隐秘信息边界后再扩展。 |
| 事件日志与 prompt | 只有骨架 | 必须确保事件表达能覆盖 FAQ 中的特例裁决和玩家选择窗口。 |
| 卡牌功能分组 | 已按官网快照生成 | 需要用 FAQ 标注特殊牌、系列规则和容易被文本误解析的场景。 |

## 6. 后续开发门禁

任何规则能力进入 `ENGINE_READY` 或 `CARD_VALIDATED` 前，必须满足：

- 已查看五份 PDF 中与该能力相关的章节或 FAQ。
- 已记录规则依据，至少包括 PDF 文件名和页码或章节标识。
- 已确认官网卡面原文。
- 已写出 command log -> events -> snapshots 的测试。
- 若旧 Java 不覆盖或覆盖错误，必须建立手工 fixture 或新的 C# expected fixture。
- 若发现旧 Java 行为错误，优先修正新项目设计；旧 Java 只补导出或对照，不强行改成新权威。

## 7. 立即调整

P1 下一步不直接扩展更多玩法规则，先完成：

1. 抽取五份 PDF 的目录、关键词和 FAQ 问题索引。
2. 给现有 `PASS`、`END_TURN`、重复 `PASS` 三条 fixture 增加规则依据记录。
3. 把 fixture 语义从单纯 `java-oracle` 扩展为 `rulesEvidence + legacyOracle + expected`。
4. 建立 `NEEDS_RULE_AUDIT` 状态，防止已开发能力绕过 FAQ 重审。

当前已开发内容的逐项状态见 `docs/development-audit-status.md`。
