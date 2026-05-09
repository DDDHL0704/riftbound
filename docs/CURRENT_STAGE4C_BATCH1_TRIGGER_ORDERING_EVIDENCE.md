# Stage 4C-1 Trigger Ordering Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-1 / E 卡牌覆盖矩阵 overlay**

结论：**4C-1 部分降低 `ORDER_TRIGGERS` / battle initial stack / hidden trigger metadata blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- 本文不实时抓官网，不依赖 `cardQaList`。
- backend 事实来源：B 4C-1 变更摘要 + A full test 结果。
- A full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` => 3337/3337 passed。

## 2. 4C-1 Closed Slice

| 域 | 4C-1 已有证据 |
|---|---|
| `ORDER_TRIGGERS` | 从 3D 最小 runtime window 升级为 APNAP conservative controller-block 子集。 |
| prompt metadata | `triggerIds` = raw queue order；`orderedTriggerIds` = 服务端推荐合法 APNAP resolution top-first 提交顺序。 |
| legality constraints | `orderingPolicy=APNAP_CONTROLLER_BLOCKS_CONSERVATIVE`、`orderedTriggerIdsSemantics=STACK_RESOLUTION_ORDER_TOP_FIRST`、`controllerBlockOrder`、`legalResolutionControllerBlockOrder`。 |
| reorder validation | 跨控制者 block 不可非法重排；同控制者 block 内可重排；失败零副作用。 |
| stack handoff | 合法排序进入 `StackItems`，随后进入 stack priority。 |
| battle initial stack | active battle attacker / defender initial triggers 有代表测试进入 `ORDER_TRIGGERS`。 |
| hidden metadata | 不可见 face-down standby trigger source 在 prompt / snapshot 中按 viewer 脱敏。 |

## 3. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch1TriggerOrdering`
- `functionalUnits[].stage4C1`
- `fieldDefinitions.functionalUnits.stage4C1`
- `fieldDefinitions.stage4CBatch1TriggerOrdering`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]`

`stage4C1` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。

## 4. Counts

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
| recommended trigger-ordering pressure FUs | 32 |
| full-official upgrades | 0 |

## 5. Top20 Partially Reduced

这些 high-risk FUs 的 `ORDER_TRIGGERS` / battle initial stack blocker 被 4C-1 部分降低，但仍不能升级 full-official：

| FU | Representative | 4C-1 delta |
|---|---|---|
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | order trigger / battle initial stack representative blocker partially reduced; FAQ + engine blockers remain. |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | order trigger / combat movement pressure partially reduced; full battle/damage + FAQ blockers remain. |
| `FU-964b214448` | `SFD·020/221` 德莱文 | order trigger / battle body pressure partially reduced; FAQ + engine blockers remain. |
| `FU-05ce012700` | `SFD·218/221` 沉没神庙 | battlefield / hidden metadata pressure partially reduced; full battlefield scoring + FAQ blockers remain. |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | attack trigger / reveal / hidden metadata pressure partially reduced; real trigger enqueue + FAQ blockers remain. |
| `FU-813144e7d4` | `OGN·168/298` 战或逃 | battlefield movement / hidden metadata pressure partially reduced; full movement + FAQ blockers remain. |

## 6. Recommended Pressure FUs

后续 `ORDER_TRIGGERS` / battle initial stack / trigger ordering 压测首批仍建议从这些 FUs 开始：

`FU-104211dbbc`、`FU-2dca1ad450`、`FU-964b214448`、`FU-05ce012700`、`FU-422b450261`、`FU-813144e7d4`、`FU-50ceb593ab`、`FU-8dae5c40be`、`FU-201e46695b`、`FU-f076dbf9ee`、`FU-f9f5c508c0`、`FU-4e2e19359f`、`FU-f9eb8c6f71`、`FU-5164c0d190`、`FU-c027639a3c`、`FU-16d3a6dd4e`、`FU-3e9cb3904e`、`FU-7d0b8868b7`、`FU-1563edad5f`、`FU-67c6b0186e`、`FU-bf81341dd2`、`FU-5cea85e7c3`、`FU-e3dcc3b30f`、`FU-7f4a387b92`。

完整 67 个受影响 FU 清单以 `stage4CBatch1TriggerOrdering.affectedFunctionalUnitIds` 为准。

## 7. Next Batch Suggestion

1. `4C-2` complete trigger engine + real card-trigger enqueue。
2. `4C-3` trigger payment / decline / payment failure。
3. `4C-4` FAQ adjudication for trigger / battle ordering。
4. `4C-5` battle initial stack + `ASSIGN_COMBAT_DAMAGE` pressure matrix。
5. `4C-6` hidden information and viewer redaction regression pack。

## 8. Still Missing P0/P1

- 完整 trigger engine。
- 真实卡牌触发全规则入队。
- trigger payment / decline / payment failure handling。
- 完整 APNAP 多玩家独立排序，以及 4C-1 保守 controller-block 子集之外的复杂排序。
- 完整 battle initial stack rule matrix。
- FAQ adjudication 与 ruling-backed tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。

是否允许批量 full-official 覆盖：**不允许。**
