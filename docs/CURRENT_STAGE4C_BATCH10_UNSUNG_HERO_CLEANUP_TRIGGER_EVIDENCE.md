# Stage 4C-10 Unsung Hero Cleanup Trigger Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-10 / E 卡牌覆盖矩阵 overlay**

结论：**4C-10 只部分降低 `SFD·167/221`《无名英雄》的 state-based cleanup powerful last-breath draw-2 trigger enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `SFD·167/221`《无名英雄》在矩阵中对应 `FU-1701d1d89a`，不是猜测。
- `OGN·029/298`《星落》在矩阵中对应 `FU-56d6b01aa1`，本批只作为 lethal damage + cleanup source，不获得 full-official。
- 矩阵规则 / FAQ 证据入口：Unsung Hero 现有关联 `SOUL-JFAQ-260114 p20`；Starfall 当前无 FAQ candidate，仍带 `FEPR/Targeting/TimingWindows` blocker。

## 2. 4C-10 Closed Slice

| 域 | 4C-10 已有证据 |
|---|---|
| verified card | `SFD·167/221`《无名英雄》 |
| verified FU | `FU-1701d1d89a` |
| supporting source card | `OGN·029/298`《星落》 |
| supporting source FU | `FU-56d6b01aa1` |
| registry effect | `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_PLAY_UNIT` |
| trigger effect | `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2` |
| supporting effect | `STARFALL_DAMAGE_3_TWICE` |
| cleanup event | `STATE_BASED_CLEANUP` / `LETHAL_DAMAGE` / `UNIT_DESTROYED` |
| runtime path | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible base-zone Unsung Hero CardObjectState.Power >= 5 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN x2` |
| negative power guard | `Power < 5` 不入队、不抽牌。 |
| visibility guard | hidden / face-down / standby Unsung Hero 不入队、不泄漏、不抽牌。 |
| non-full-official guard | 只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。 |

## 3. Powerful Boundary

| Boundary | 4C-10 handling |
|---|---|
| represented powerful check | `CardObjectState.Power >= 5` |
| below-threshold case | `Power < 5` does not enqueue and draws no cards. |
| LayerEngine / effective power | Not covered. |
| temporary modifier | Not covered. |
| battlefield objectLocation full matrix | Not covered. |
| explicit destroy | Not migrated by this overlay. |

## 4. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused RealTriggerQueue tests | 21/21 passed |
| backend full tests | 3361/3361 passed |
| frontend build | passed |
| Chrome smoke | passed |
| Stage 3 preflight | passed |

这些结果由 A 在 B 完成 4C-10 runtime 后报告；E 本轮只落矩阵 / 证据 overlay。

## 5. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch10UnsungHeroCleanupTriggerEnqueue`
- `functionalUnits[].stage4C10`，仅用于 `FU-1701d1d89a`
- `fieldDefinitions.functionalUnits.stage4C10`
- `fieldDefinitions.stage4CBatch10UnsungHeroCleanupTriggerEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-10 记录

`stage4C10` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。Starfall 只是 supporting source，不因本批升级。

## 6. Counts

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

## 7. Verified FU

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-1701d1d89a` | `SFD·167/221` 无名英雄 | 4C-10 | `POWERFUL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` / LayerEngine / complete trigger engine / FAQ adjudication remain. |

## 8. Next Pressure Candidates

这些 FUs / families 只记录为候选，未被 4C-10 标为已实现：

- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

## 9. Still Missing P0/P1

- 完整 trigger engine。
- LayerEngine / effective power / temporary modifier 的强力判定。
- battlefield objectLocation 全矩阵。
- Unsung Hero explicit destroy migration。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- hidden / face-down trigger original visibility modeling；4C-10 只验证 no-enqueue / no-draw metadata leak guard。
- FAQ adjudication 与 regression tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。
- 正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**
