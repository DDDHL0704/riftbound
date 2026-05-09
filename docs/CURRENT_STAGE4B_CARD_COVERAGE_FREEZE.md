# Stage 4B Card Coverage Freeze

日期：2026-05-09；4C-1 overlay 追加：2026-05-10

阶段：**阶段 4B / E 卡牌覆盖矩阵冻结**

结论：**4B freeze 完成；4C-1 post-freeze overlay 已记录；NOT READY；不允许 1009 张卡批量实现。**

本文冻结卡牌覆盖矩阵、official text / FAQ evidence、functional units、测试优先级和阶段 4 批量顺序。4C-1 只在冻结矩阵上追加 APNAP `ORDER_TRIGGERS` / battle initial stack / hidden trigger metadata redaction overlay。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Snapshot

本轮只读取固定官网快照：

- `data/official/card-catalog.zh-CN.json`
- `fetchedAt = 2026-04-27`
- `total = 1009`
- `.cards.length = 1009`
- unique `id` = 1009
- unique exact collector id / `cardNo` = 1009

未实时抓取官网。

## 2. Frozen Identity Rules

| 项 | 冻结口径 |
|---|---|
| `cardId` | 官方 `.cards[].id`，每个 snapshot entry 唯一。 |
| `collectorId` | 官方精确 `cardNo`；不规范化 `·`、`-`、lowercase suffix、`*`、`·P`、token collector。 |
| card entry | 官方 `.cards[]` 的一行；1009/1009 全部计入。 |
| functional unit | 行为等价 oracle 分组；当前冻结为 811 个。 |
| oracle/effectId | 当前实现映射中的 `effectKinds`；冻结为 807 个 unique ids。 |
| shared oracle | 多个 card entries 共用一个 functional unit / effect implementation；不减少 1009 card entry 计数。 |

token、rune、battlefield、promo、异画/后缀均计入 snapshot entries：

| 类别 | 数量 |
|---|---:|
| token entries | 13 |
| rune entries | 48 |
| battlefield entries | 59 |
| promo `·P` entries | 4 |
| `*` variant entries | 36 |
| lowercase suffix / alternate-art entries | 100 |
| exact collector ids | 1009 |
| base collector ids | 873 |
| base collector duplicate groups | 127 |
| base collector duplicate entries | 263 |

## 3. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已冻结以下 4B 字段：

| 区域 | 字段 |
|---|---|
| top-level | `stage4BCardCoverageFreeze` |
| snapshot entry | `stage4B.collectorId`, `stage4B.functionalUnitId`, `stage4B.freezeStatus`, `stage4B.statusFlags`, `stage4B.oracleEffectIds`, `stage4B.rulesRefs`, `stage4B.faqRefs`, `stage4B.automatedTestStatus` |
| functional unit | `stage4B.freezeStatus`, `stage4B.statusFlags`, `stage4B.effectImplementation`, `stage4B.evidence`, `stage4B.automatedTests`, `stage4B.fullOfficialBlockers` |

这使矩阵可以从 `cardId -> functionalUnitId -> oracle/effectIds -> rules / FAQ evidence -> automated tests -> status` 解释每张卡的冻结状态。

## 4. Status Counts

Primary status counts by functional unit：

| status | FUs |
|---|---:|
| `IMPLEMENTED_TESTED` | 50 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 102 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

Primary status counts by snapshot entry：

| status | entries |
|---|---:|
| `IMPLEMENTED_TESTED` | 77 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 273 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

Status flags are non-exclusive. A FU can be `IMPLEMENTED_TESTED` and still carry `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW`; this is representative evidence, not full-official.

## 5. Uncovered Summary

4B grants zero full-official coverage:

- full-official functional units = 0
- full-official snapshot entries = 0
- `stage4BCardCoverageFreeze.uncoveredSummary.fullOfficialUncoveredFunctionalUnitIds` contains 811/811 FUs.

Top uncovered / full-official blockers:

| FU | Representative | status | reason |
|---|---|---|---|
| `FU-fb79eea7fc` | `OGN·077/298` 中娅沙漏 | `NEEDS_FAQ_REVIEW` | FAQ + cleanup/replacement + payment + hidden/zone risk. |
| `FU-2653af0380` | `OGN·242/298` 海兽钓钩 | `NEEDS_FAQ_REVIEW` | FAQ + layer/payment/hidden/zone risk. |
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | `IMPLEMENTED_TESTED` | Representative evidence exists, but engine + FAQ blockers remain. |
| `FU-964b214448` | `SFD·020/221` 德莱文 | `IMPLEMENTED_TESTED` | Representative evidence exists, but engine + FAQ blockers remain. |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | `IMPLEMENTED_TESTED` | Combat damage / movement / FAQ blockers remain. |
| `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | `IMPLEMENTED_TESTED` | spellshield / stun / spell duel blockers remain. |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | `IMPLEMENTED_TESTED` | attack trigger / hidden info / ordering blockers remain. |
| `FU-05ce012700` | `SFD·218/221` 沉没神庙 | `IMPLEMENTED_TESTED` | battlefield / scoring / FAQ blockers remain. |

Full uncovered lists are in:

- `stage4BCardCoverageFreeze.uncoveredSummary.blockingFunctionalUnitIdsByPrimaryStatus`
- `stage4BCardCoverageFreeze.uncoveredSummary.blockingFunctionalUnitIdsByFlag`
- `stage4BCardCoverageFreeze.uncoveredSummary.fullOfficialUncoveredFunctionalUnitIds`

## 6. Top Risk Summary

Top risk remains the Stage 2 Top20 with 4B statuses overlaid:

| Rank | FU | Representative | 4B primary status |
|---:|---|---|---|
| 1 | `FU-fb79eea7fc` | `OGN·077/298` 中娅沙漏 | `NEEDS_FAQ_REVIEW` |
| 2 | `FU-2653af0380` | `OGN·242/298` 海兽钓钩 | `NEEDS_FAQ_REVIEW` |
| 3 | `FU-104211dbbc` | `SFD·148/221` 德莱文 | `IMPLEMENTED_TESTED` |
| 4 | `FU-964b214448` | `SFD·020/221` 德莱文 | `IMPLEMENTED_TESTED` |
| 5 | `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | `IMPLEMENTED_TESTED` |
| 6 | `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | `IMPLEMENTED_TESTED` |
| 7 | `FU-422b450261` | `SFD·170/221` 雷克塞 | `IMPLEMENTED_TESTED` |
| 8 | `FU-05ce012700` | `SFD·218/221` 沉没神庙 | `IMPLEMENTED_TESTED` |
| 9 | `FU-1945f6918c` | `SFD·029/221` 雷克塞 | `IMPLEMENTED_TESTED` |
| 10 | `FU-813144e7d4` | `OGN·168/298` 战或逃 | `IMPLEMENTED_TESTED` |

## 7. 4C Batch Order Suggestion

4B freeze 当时不进入 4C；在 4C-1 overlay 追加后，后续批量顺序建议更新为：

1. P0/P1 engine support blockers：先收完整 trigger engine、full damage assignment、PaymentEngine、battle/spell duel lifecycle、LayerEngine/replacement/prevention、hidden info、control/zone movement。
2. FAQ adjudication and ruling-backed tests：把 PDF/FAQ 候选判定为适用 / 不适用 / 通用规则，再补 FAQ tests。
3. Reusable oracle/effectId clusters：先锁 2+ card entries 的 shared oracle 映射，确认所有 member `cardId/cardNo`。
4. Implemented but untested direct FUs：补简单 direct FU 的正/反测试。
5. Representative tested FUs upgrade review：在 blocker 清零后，逐 FU / card entry 审核是否可升 full-official。

## 8. Post-Freeze 4C-1 Overlay

4C-1 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过 full test 的局部 runtime 证据：

| 项 | 4C-1 记录 |
|---|---|
| `ORDER_TRIGGERS` | 从 3D 最小 runtime window 升级为 `APNAP_CONTROLLER_BLOCKS_CONSERVATIVE` controller-block 子集。 |
| prompt metadata | `triggerIds` = raw queue order；`orderedTriggerIds` = 服务端推荐合法 APNAP resolution top-first 提交顺序。 |
| legality | `crossControllerReorderingAllowed=false`，`withinControllerReorderingAllowed=true`；非法跨控制者 block 重排零副作用失败。 |
| battle initial stack | active battle attacker / defender initial trigger 有代表测试进入 `ORDER_TRIGGERS`，再进入 stack priority。 |
| hidden metadata | 不可见 face-down standby trigger source 在 prompt / snapshot 中按 viewer 脱敏。 |
| backend validation | `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3337/3337。 |

矩阵 overlay 数字：

- `ORDER_TRIGGERS` dependency FUs：67
- `stage4C1` tagged FUs：67
- Top20 中 blocker 被部分降低的 FUs：6（`FU-104211dbbc`、`FU-2dca1ad450`、`FU-964b214448`、`FU-05ce012700`、`FU-422b450261`、`FU-813144e7d4`）
- battle initial stack pressure FUs：31
- hidden trigger metadata redaction candidates：11
- full-official upgrades：0

仍缺：完整 trigger engine、真实卡牌全触发生成、trigger payment / decline / payment failure、完整 APNAP 多玩家独立排序、FAQ adjudication、完整 battle initial stack 和 1009/811 full-official 覆盖。

下一批建议：优先做完整 trigger engine + real card-trigger enqueue，其次 trigger payment / decline，再做 FAQ adjudication + battle/damage pressure + hidden-info regression。

## 9. 4B / 4C-1 Blocker

4B freeze 和 4C-1 overlay 本身无文档阻断。仍阻断 READY / full-official 的事项：

- 0/811 functional units 获得 full-official。
- P0/P1 engine support 仍影响 762 FUs by status flag。
- FAQ review 仍影响 179 FUs by status flag。
- Representative automated evidence 不是 1009 张卡 full-official tests。
- 4C 后续批量实现仍需要 A 另行授权写入锁。

是否允许进入卡牌效果批量覆盖：**不允许。**
