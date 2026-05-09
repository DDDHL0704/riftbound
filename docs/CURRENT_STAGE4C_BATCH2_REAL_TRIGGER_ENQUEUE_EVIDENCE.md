# Stage 4C-2 Real Trigger Enqueue Evidence Overlay

日期：2026-05-09

阶段：**阶段 4C-2 / E 卡牌覆盖矩阵 overlay**

结论：**4C-2 只部分降低 Watchful Sentinel real card-trigger enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `OGN·096/298`《警觉的哨兵》在矩阵中对应 `FU-67568b793d`，不是猜测。
- backend 事实来源：B 4C-2 变更摘要 + A 验证结果。
- B 变更范围：`src/Riftbound.Engine/CoreRuleEngine.cs`，新增 `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`。

## 2. 4C-2 Closed Slice

| 域 | 4C-2 已有证据 |
|---|---|
| verified card | `OGN·096/298`《警觉的哨兵》 |
| verified FU | `FU-67568b793d` |
| registry effect | `WATCHFUL_SENTINEL_PLAY_UNIT` |
| trigger effect | `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` |
| source event | real `UNIT_DESTROYED` |
| runtime path | `TriggerQueue -> ORDER_TRIGGERS prompt -> StackItems -> pass priority -> TRIGGER_RESOLVED / CARD_DRAWN` |
| APNAP default order | 多 Watchful 跨控制者默认 `orderedTriggerIds` 可直接提交 accepted。 |
| illegal order | 非法跨控制者排序拒绝且 no mutation。 |
| compatibility | 单个 Watchful Sentinel 仍保留旧即时结算兼容路径。 |

## 3. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused tests | 11/11 passed |
| backend full tests | 3338/3338 passed |
| frontend build | passed |
| Chrome smoke | passed |
| stage3 preflight | passed |

## 4. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch2RealTriggerEnqueue`
- `functionalUnits[].stage4C2`，仅用于 `FU-67568b793d`
- `fieldDefinitions.functionalUnits.stage4C2`
- `fieldDefinitions.stage4CBatch2RealTriggerEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-2 记录

`stage4C2` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。

## 5. Counts

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C2` verified FUs | 1 |
| `stage4C2` verified snapshot entries | 1 |
| next-pressure candidate FUs | 24 |
| full-official upgrades | 0 |

## 6. Verified FU

| FU | Representative | overlay status | still blocked |
|---|---|---|---|
| `FU-67568b793d` | `OGN·096/298` 警觉的哨兵 | `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` remain; full-official not granted. |

## 7. Next Pressure Candidates

这些 FUs 只记录为候选，未被 4C-2 标为已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- last-breath：`FU-3acf92c924`、`FU-6a52b04cb2`、`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-b829fb32b9`、`FU-16d3a6dd4e`、`FU-4e2e19359f`、`FU-f9eb8c6f71`、`FU-1701d1d89a`
- on-play registered trigger：`FU-d5e1143438`、`FU-bf81341dd2`、`FU-e8d8846d73`、`FU-808f8b89db`、`FU-f18a49e06d`、`FU-67c6b0186e`
- attack / defense / conquer：`FU-661793867e`、`FU-5cea85e7c3`、`FU-422b450261`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-3e9cb3904e`

## 8. Still Missing P0/P1

- 完整 trigger engine。
- 其他 last-breath / destroyed-family functional units。
- trigger payment / decline / payment failure handling。
- state-based cleanup trigger enqueue。
- FAQ adjudication 与 ruling-backed tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。

是否允许批量 full-official 覆盖：**不允许。**
