# Stage 4C-6 Honest Broker Cleanup Trigger Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-6 / E 卡牌覆盖矩阵 overlay**

结论：**4C-6 只部分降低 `SFD·155/221`《诚实掮客》的 state-based cleanup trigger enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `SFD·155/221`《诚实掮客》在矩阵中对应 `FU-3acf92c924`，不是猜测。
- `OGN·029/298`《星落》在矩阵中对应 `FU-56d6b01aa1`，本批只作为致命伤害来源，不获得 full-official。
- 矩阵规则 / FAQ 证据入口：Honest Broker 现有关联 `CORE-260330 p79`；Starfall 当前无 FAQ candidate，仍带 `FEPR/Targeting/TimingWindows` blocker。

## 2. 4C-6 Closed Slice

| 域 | 4C-6 已有证据 |
|---|---|
| verified card | `SFD·155/221`《诚实掮客》 |
| verified FU | `FU-3acf92c924` |
| supporting source card | `OGN·029/298`《星落》 |
| supporting source FU | `FU-56d6b01aa1` |
| registry effect | `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT` |
| trigger effect | `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` |
| supporting effect | `STARFALL_DAMAGE_3_TWICE` |
| cleanup event | `STATE_BASED_CLEANUP` / `LETHAL_DAMAGE` |
| runtime path | `Starfall damage -> state-based cleanup LETHAL_DAMAGE -> visible Honest Broker HONEST_BROKER_LAST_BREATH_CREATE_GOLD -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED` |
| visibility guard | hidden / face-down / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏。 |

## 3. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused RealTriggerQueue tests | 6/6 passed |
| backend full tests | 3348/3348 passed |
| frontend build | passed |
| Chrome smoke | passed |
| Stage 3 preflight | passed |

这些结果由 A 在 B 完成 4C-6 runtime 后报告；E 本轮只落矩阵 / 证据 overlay。

## 4. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch6HonestCleanupTriggerEnqueue`
- `functionalUnits[].stage4C6`，仅用于 `FU-3acf92c924`
- `fieldDefinitions.functionalUnits.stage4C6`
- `fieldDefinitions.stage4CBatch6HonestCleanupTriggerEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-6 记录

`stage4C6` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。4C-3 的 `stage4C3` overlay 保留不回退。

## 5. Counts

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

## 6. Verified FUs

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-3acf92c924` | `SFD·155/221` 诚实掮客 | 4C-6 | `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / complete trigger engine / hidden-origin trigger model / FAQ adjudication remain. |

`FU-56d6b01aa1` / `OGN·029/298`《星落》只是 supporting source。它仍保留 4B `NEEDS_ENGINE_SUPPORT` 状态，不因 4C-6 获得 verified overlay。

## 7. Next Pressure Candidates

这些 FUs / families 只记录为候选，未被 4C-6 标为已实现：

- Poro conditional draw：`FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`
- simple state cleanup last-breath：`FU-0500c77a70`、`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

## 8. Still Missing P0/P1

- 完整 trigger engine。
- visible Watchful / Honest cleanup slices 之外的 last-breath / destroyed / friendly-destroyed functional units。
- hidden / face-down trigger original visibility modeling；4C-6 只验证 no-enqueue / no-token metadata leak guard。
- FAQ adjudication 与 regression tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。
- 正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**
