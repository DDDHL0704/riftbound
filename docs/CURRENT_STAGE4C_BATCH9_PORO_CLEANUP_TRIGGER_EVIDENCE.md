# Stage 4C-9 Poro Cleanup Trigger Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-9 / E 卡牌覆盖矩阵 overlay**

结论：**4C-9 只部分降低 Sad/Loyal Poro 条件抽牌 state-based cleanup trigger enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `SFD·036/221`《哀哀魄罗》在矩阵中对应 `FU-f8bfd5c6f9`，不是猜测。
- `UNL-221/219`《哀哀魄罗》在矩阵中对应 `FU-938b749c23`，不是猜测。
- `UNL-156/219`《忠忠魄罗》在矩阵中对应 `FU-0415e3b46d`，不是猜测。
- `OGN·029/298`《星落》在矩阵中对应 `FU-56d6b01aa1`，本批只作为 lethal damage + cleanup source，不获得 full-official。
- 矩阵规则 / FAQ 证据入口：Sad Poro 现有关联 `SOUL-JFAQ-260114 p13`；Loyal Poro 当前无 FAQ candidate，仍需后续 D/用户 adjudication。

## 2. 4C-9 Closed Slice

| 域 | 4C-9 已有证据 |
|---|---|
| verified cards | `SFD·036/221`《哀哀魄罗》；`UNL-221/219`《哀哀魄罗》；`UNL-156/219`《忠忠魄罗》 |
| verified FUs | `FU-f8bfd5c6f9`；`FU-938b749c23`；`FU-0415e3b46d` |
| supporting source card | `OGN·029/298`《星落》 |
| supporting source FU | `FU-56d6b01aa1` |
| Sad trigger effect | `SAD_PORO_LAST_BREATH_DRAW_1` |
| Loyal trigger effect | `LOYAL_PORO_LAST_BREATH_DRAW_1` |
| supporting effect | `STARFALL_DAMAGE_3_TWICE` |
| cleanup event | `STATE_BASED_CLEANUP` / `LETHAL_DAMAGE` / `UNIT_DESTROYED` |
| runtime path | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible base-zone Poro condition -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN` |
| visibility guard | hidden / face-down / standby Poro 不入队、不泄漏、不抽牌。 |
| non-full-official guard | 同时死亡落单判定未 full-official；本批只做 guarded visible cleanup slice。 |

## 3. Condition Boundary

| FU | Condition in 4C-9 | Still not full-official |
|---|---|---|
| `FU-f8bfd5c6f9` | Sad Poro：同位置无其他友方正面非待命单位时抽 1。 | 同时死亡时的落单判定仍需 FAQ/rules adjudication。 |
| `FU-938b749c23` | Sad Poro：同位置无其他友方正面非待命单位时抽 1。 | 同时死亡时的落单判定仍需 FAQ/rules adjudication。 |
| `FU-0415e3b46d` | Loyal Poro：同位置有其他友方正面非待命单位，且该其他友方不在本轮 cleanup removal set 时抽 1。 | Loyal Poro 无矩阵 FAQ candidate；仍需 D/用户确认其与“落单”反条件的官方裁定。 |

## 4. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused RealTriggerQueue tests | 21/21 passed |
| backend full tests | 3358/3358 passed |
| frontend build | passed |
| Chrome smoke | passed |
| Stage 3 preflight | passed |

这些结果由 A 在 B 完成 4C-9 runtime 后报告；E 本轮只落矩阵 / 证据 overlay。

## 5. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch9PoroCleanupTriggerEnqueue`
- `functionalUnits[].stage4C9`，仅用于 `FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`
- `fieldDefinitions.functionalUnits.stage4C9`
- `fieldDefinitions.stage4CBatch9PoroCleanupTriggerEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-9 记录

`stage4C9` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。Starfall 只是 supporting source，不因本批升级。

## 6. Counts

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

## 7. Verified FUs

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-f8bfd5c6f9` | `SFD·036/221` 哀哀魄罗 | 4C-9 | `CONDITIONAL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` / complete trigger engine / FAQ adjudication remain. |
| `FU-938b749c23` | `UNL-221/219` 哀哀魄罗 | 4C-9 | `CONDITIONAL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` / complete trigger engine / FAQ adjudication remain. |
| `FU-0415e3b46d` | `UNL-156/219` 忠忠魄罗 | 4C-9 | `CONDITIONAL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / no FAQ candidate in matrix / complete trigger engine remain. |

## 8. Next Pressure Candidates

这些 FUs / families 只记录为候选，未被 4C-9 标为已实现：

- simple state cleanup last-breath：`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

## 9. Still Missing P0/P1

- 完整 trigger engine。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- 同时死亡落单判定 full-official。
- hidden / face-down trigger original visibility modeling；4C-9 只验证 no-enqueue / no-draw metadata leak guard。
- FAQ adjudication 与 regression tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。
- 正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**
