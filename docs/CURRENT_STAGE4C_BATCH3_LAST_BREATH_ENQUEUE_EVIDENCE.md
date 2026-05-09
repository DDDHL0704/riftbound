# Stage 4C-3 Last-Breath Enqueue Evidence Overlay

日期：2026-05-09

阶段：**阶段 4C-3 / E 卡牌覆盖矩阵 overlay**

结论：**4C-3 只部分降低 Honest Broker last-breath real enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `SFD·155/221`《诚实掮客》在矩阵中对应 `FU-3acf92c924`，不是猜测。
- backend 事实来源：B 4C-3 变更摘要 + A 验证结果。
- B 变更范围：`src/Riftbound.Engine/CoreRuleEngine.cs` 和 `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`。

## 2. 4C-3 Closed Slice

| 域 | 4C-3 已有证据 |
|---|---|
| verified card | `SFD·155/221`《诚实掮客》 |
| verified FU | `FU-3acf92c924` |
| registry effect | `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT` |
| trigger effect | `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` |
| source event | real `UNIT_DESTROYED` |
| runtime path | `TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED` |
| compatibility | 单个 Watchful / Honest Broker 仍保留旧即时结算兼容路径。 |
| ordering window | 多个官方化 last-breath 触发同时产生时进入排序窗口。 |

## 3. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused tests | 13/13 passed |
| backend full tests | 3339/3339 passed |
| frontend build | passed |
| Chrome smoke | passed |
| stage3 preflight | passed |

## 4. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch3LastBreathEnqueue`
- `functionalUnits[].stage4C3`，仅用于 `FU-3acf92c924`
- `fieldDefinitions.functionalUnits.stage4C3`
- `fieldDefinitions.stage4CBatch3LastBreathEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-3 记录

`stage4C3` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。

## 5. Counts

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

## 6. Verified FUs

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-67568b793d` | `OGN·096/298` 警觉的哨兵 | 4C-2 | `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` remain. |
| `FU-3acf92c924` | `SFD·155/221` 诚实掮客 | 4C-3 | `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` remains. |

## 7. Next Pressure Candidates

这些 FUs 只记录为候选，未被 4C-3 标为已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- last-breath：`FU-6a52b04cb2`、`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-b829fb32b9`、`FU-16d3a6dd4e`、`FU-4e2e19359f`、`FU-f9eb8c6f71`、`FU-1701d1d89a`
- on-play registered trigger：`FU-d5e1143438`、`FU-bf81341dd2`、`FU-e8d8846d73`、`FU-808f8b89db`、`FU-f18a49e06d`、`FU-67c6b0186e`
- attack / defense / conquer：`FU-661793867e`、`FU-5cea85e7c3`、`FU-422b450261`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-3e9cb3904e`

## 8. Still Missing P0/P1

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- state-based cleanup trigger enqueue。
- trigger payment / decline / payment failure handling。
- FAQ adjudication 与 ruling-backed tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。

是否允许批量 full-official 覆盖：**不允许。**
