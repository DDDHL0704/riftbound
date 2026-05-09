# 阶段 3D 卡牌证据：ORDER_TRIGGERS / Complex Prompt Dependency

日期：2026-05-09

阶段：**阶段 3D / E 卡牌覆盖矩阵与 ORDER_TRIGGERS 证据 overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文是 E 的卡牌覆盖矩阵 / FAQ 证据文档。B 已在 3D 关闭 `ORDER_TRIGGERS` 最小 runtime window：prompt、`orderedTriggerIds` command、validation、合法排序入 `StackItems`、事件日志。E 只维护 dependency index、阶段 4 优先级、FAQ 互动候选、压测卡组和可复用 oracle / effectId 候选；不修改服务端、前端、测试、审计主文档或 `riftbound-dotnet.sln`。

## 1. 3D 范围与禁区

3D 只标记：

- 依赖 `PAY_COST` 的 functional units。
- 依赖 `ASSIGN_COMBAT_DAMAGE` 的 functional units。
- 依赖 `ORDER_TRIGGERS` / battle initial stack / trigger ordering 的 functional units。
- 依赖 battlefield / control / conquer lifecycle 的 functional units。
- 依赖 spell duel / battle lifecycle 的 functional units。
- 阶段 4 high-risk、FAQ、压测卡组、可复用 oracle / effectId 候选。

3D 明确不做：

- READY 标记。
- 最终验收版 18 步 E2E。
- 1009 entries / 811 functional units full-official 覆盖。
- 批量生成或修改 `CardBehaviorDefinition`。
- 完整 trigger engine、真实卡牌触发全规则入队、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment。
- 服务端 C#、前端、测试、A checkpoint、D 审计文档或 `riftbound-dotnet.sln` 改动。

## 2. 机器矩阵字段

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- `stage3DComplexPromptLifecycle`
- `stage3DComplexPromptLifecycle.dependencyIndexes.*.functionalUnitIds`
- `stage3DComplexPromptLifecycle.stage4Priorities`
- `stage3DComplexPromptLifecycle.recommendedOrderTriggersBattleInitialStackTriggerOrderingFunctionalUnits`
- `functionalUnits[].stage3D.dependencyBuckets`
- `functionalUnits[].stage3D.phase4Roles`
- `functionalUnits[].stage3D.useBoundary`

完整 FU 清单以 JSON 的 `functionalUnitIds` 为准；本文只列人读摘要和首批优先项。

## 3. Dependency Buckets

| 依赖桶 | FU 数 | 代表来源阶段 | 3D 结论 |
|---|---:|---|---|
| `PAY_COST` | 370 | 3A | 3A 仅关闭最小 runtime；完整 PaymentEngine、额外/替代费用和官方支付窗口仍阻断 full-official。 |
| `ASSIGN_COMBAT_DAMAGE` | 287 | 3C | 3C 仅关闭最小 runtime；完整 damage assignment choices、constraints、barrier/back-row/negative-power 矩阵仍未完成。 |
| `ORDER_TRIGGERS` / battle initial stack | 67 | 3D | 最小 runtime window 已关闭；完整 trigger engine、真实卡牌触发入队、APNAP / 跨控制者排序、battle initial stack 全规则、trigger cost / decline / payment 仍未关闭。 |
| battlefield / control / conquer | 358 | 3B | 3B 仅覆盖 lifecycle 证据；control freeze/release、standby、conquer/hold scoring 仍需后续闭合。 |
| spell duel / battle | 288 | 3C | 3C 仅覆盖 battle/damage 证据；完整 spell duel、battle task、attack/defense identity 仍未 full-official。 |
| FAQ 命中候选 | 179 | 阶段 2-3D | 必须逐项 adjudicate 为适用 / 不适用 / 通用规则，不能直接当作实现证明。 |
| 可复用 oracle / effectId 候选 | 113 | 阶段 2-3D | 可以节省实现量，但每个 card entry 到 FU / implementation / test / FAQ 的映射仍必须显式完整。 |

## 4. Stage 4 Priority

Stage 4 优先级分四类：

| 类别 | 机器字段 | 用途 |
|---|---|---|
| high-risk FUs | `stage4Priorities.highRiskFunctionalUnits` | 先审 Top20 复合风险，尤其 FAQ + 支付 + battle/control/hidden-info 交叉。 |
| FAQ 互动 | `stage4Priorities.faqInteractionFunctionalUnits` | 先做 PDF/FAQ adjudication，再补回归测试。 |
| 压测卡组 | `stage4Priorities.phase4PressureDeckFunctionalUnits` | 组合 3A/3B/3C/3D 代表路径，压测 prompt/lifecycle。 |
| 可复用 oracle / effectId | `stage4Priorities.reusableOracleEffectImplementationCandidates` | 优先把可复用 implementation 与 2+ card entries 的映射做完整。 |

首批 high-risk FUs 仍为 Top20：`FU-fb79eea7fc`、`FU-2653af0380`、`FU-104211dbbc`、`FU-964b214448`、`FU-2dca1ad450`、`FU-9f7cb73dc4`、`FU-422b450261`、`FU-05ce012700`、`FU-1945f6918c`、`FU-813144e7d4`、`FU-464ec8c275`、`FU-6e7d0dba2c`、`FU-0b6332bbf0`、`FU-6308c2db01`、`FU-7419ee7d9d`、`FU-00ee09c2cc`、`FU-b05eda44ce`、`FU-081d97eb3e`、`FU-804412488c`、`FU-9a623b3185`。

## 5. Phase 4 Pressure Deck

3D 推荐阶段 4 压测卡组只用于 focused tests，不是官方完整卡组：

| 目的 | FUs |
|---|---|
| `PAY_COST` / resource prompt | `FU-b646702ec0`, `FU-0ec69ae7e6`, `FU-39041f4562`, `FU-95b4531e4e` |
| battlefield / control / conquer | `FU-05ce012700`, `FU-00ee09c2cc`, `FU-813144e7d4`, `FU-8dae5c40be`, `FU-e3dcc3b30f`, `FU-7f4a387b92`, `FU-c027639a3c` |
| spell duel / battle / assignment | `FU-fda6183f9d`, `FU-6582231b22`, `FU-44f29ad8f7`, `FU-104211dbbc`, `FU-964b214448`, `FU-2dca1ad450` |
| combat trick / target legality / status | `FU-50ceb593ab`, `FU-201e46695b`, `FU-4e1eb0d231`, `FU-f9f5c508c0`, `FU-ee886701e4`, `FU-5164c0d190`, `FU-4329e00e20` |
| trigger ordering | `FU-422b450261`, `FU-67c6b0186e`, `FU-bf81341dd2`, `FU-5cea85e7c3`, `FU-c170628e3a`, `FU-16d3a6dd4e`, `FU-4e2e19359f` |

## 6. ORDER_TRIGGERS 压测清单

以下 FUs 适合后续专门压测 `ORDER_TRIGGERS` / battle initial stack / trigger ordering。3D 已有最小 runtime window，但这些候选仍不是 full-official：

| FU | 代表卡 | 压测点 |
|---|---|---|
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | high-risk battle body / FAQ / payment-adjacent ordering。 |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | combat damage 后移动与 trigger ordering。 |
| `FU-964b214448` | `SFD·020/221` 德莱文 | simpler battle body baseline。 |
| `FU-05ce012700` | `SFD·218/221` 沉没神庙 | battlefield-domain / scoring 与 battle stack。 |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | attack trigger、reveal、hidden-info ordering。 |
| `FU-813144e7d4` | `OGN·168/298` 战或逃 | battle/zone movement cleanup after stack。 |
| `FU-50ceb593ab` | `OGN·057/298` 格挡 | defender combat trick、barrier、damage assignment pressure。 |
| `FU-8dae5c40be` | `OGN·121/298` 提莫 | standby defend trigger、reveal during battle。 |
| `FU-201e46695b` | `SFD·003/221` 血性冲刺 | attacking combat trick、overwhelm/power modifier。 |
| `FU-f076dbf9ee` | `UNL-145/219` 派克 | standby / back-row / gold static pressure。 |
| `FU-f9f5c508c0` | `UNL-134/219` 存在焦虑 | attacking enemy target legality and movement after stun。 |
| `FU-4e2e19359f` | `UNL-179/219` 峡谷先锋 | last-breath move ordering。 |
| `FU-c027639a3c` | `OGN·035/298` 薇恩 | conquer recall ordering。 |
| `FU-16d3a6dd4e` | `SFD·165/221` 戈拉斯克调酒师 | last-breath / cleanup trigger。 |
| `FU-67c6b0186e` | `SFD·049/221` 厄斐琉斯 | weapon selection trigger。 |
| `FU-bf81341dd2` | `OGN·103/298` 拉文布鲁姆学生 | spell trigger。 |
| `FU-5cea85e7c3` | `SFD·128/221` 狂热粉丝 | defense trigger / initial battle stack。 |
| `FU-7f4a387b92` | `OGN·056/298` 自适应机器人 | conquer trigger / boon ordering。 |
| `FU-c170628e3a` | `UNL-065/219` 冰谷弓箭手 | attack trigger with payment / decline pressure。 |
| `FU-47beedf8a4` | `OGN·107/298` 斥候标兵 艾娃 | standby attack trigger。 |

## 7. Reusable Oracle / EffectId Candidates

可复用不等于 full-official。以下 FUs 代表一个 implementation 可覆盖多个官方条目，但仍需逐 card entry 显式映射和测试：

| FU | 代表卡 | entries | 风险 |
|---|---|---:|---:|
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | 3 | 103 |
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | 2 | 104 |
| `FU-964b214448` | `SFD·020/221` 德莱文 | 2 | 103 |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | 2 | 94 |
| `FU-1945f6918c` | `SFD·029/221` 雷克塞 | 2 | 93 |
| `FU-8dae5c40be` | `OGN·121/298` 提莫 | 4 | 82 |
| `FU-67c6b0186e` | `SFD·049/221` 厄斐琉斯 | 3 | 66 |
| `FU-6308c2db01` | `OGN·269/298` 腕豪 | 3 | 89 |
| `FU-6e7d0dba2c` | `SFD·187/221` 虚空遁地兽 | 2 | 89 |
| `FU-a348cbb2e2` | `UNL-199/219` 诡术妖姬 | 3 | 76 |

## 8. Still Blocking

P0/P1 仍存在：

- `ORDER_TRIGGERS` 完整 trigger engine：真实卡牌触发全规则入队、APNAP / 跨控制者复杂排序、trigger batch ordering 全矩阵仍未完成。
- battle initial stack：attack / defense triggers、spell duel focus、stack closure 与回到 battle task 仍需端到端证明。
- trigger cost / decline / payment：带费用触发、拒绝支付、支付失败零副作用仍未完成。
- `PAY_COST` 完整模型：3A 只是最小 runtime，不覆盖全部费用、额外费用、替代费用和装备/急速/装配链。
- `ASSIGN_COMBAT_DAMAGE` 完整矩阵：barrier、back-row、exclusive constraints、negative-power、simultaneous damage 和 zero-side-effect reject 仍未全覆盖。
- battlefield / control / conquer：control freeze/release、lost standby cleanup、conquer/hold scoring、replacement/prevention 与 hidden info 仍未 full-official。
- 1009 entries / 811 FUs 的 official text、FAQ adjudication、implementation、tests 仍未闭环。

是否允许进入卡牌效果批量覆盖：**不允许。**
