# Stage 4C-11 Ghostly Centaur Cleanup Trigger Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-11 / E 卡牌覆盖矩阵 overlay**

结论：**4C-11 只部分降低 `UNL-068/219`《幽魂半人马》的 state-based cleanup friendly-destroyed power trigger enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `UNL-068/219`《幽魂半人马》在矩阵中对应 `FU-0f2c4a3ea5`，不是猜测。
- `OGN·029/298`《星落》在矩阵中对应 `FU-56d6b01aa1`，本批只作为 lethal damage + cleanup source，不获得 full-official。
- 本批不覆盖 `FU-b5cb36a5c9` Viktor、`FU-c146331876` Resonant Soul、`FU-af8b05c294` Kogmaw、`FU-ee1dfb3ed3` Karthus、`FU-6a52b04cb2` Undercover Agent。

## 2. 4C-11 Closed Slice

| 域 | 4C-11 已有证据 |
|---|---|
| verified card | `UNL-068/219`《幽魂半人马》 |
| verified FU | `FU-0f2c4a3ea5` |
| supporting source card | `OGN·029/298`《星落》 |
| supporting source FU | `FU-56d6b01aa1` |
| registry effect | `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_PLAY_UNIT` |
| trigger effect | `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2` |
| supporting effect | `STARFALL_DAMAGE_3_TWICE` |
| cleanup event | `STATE_BASED_CLEANUP` / `LETHAL_DAMAGE` / `UNIT_DESTROYED` |
| runtime path | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE / UNIT_DESTROYED -> visible surviving friendly Ghostly source -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> POWER_MODIFIED_UNTIL_END_OF_TURN +2` |
| source guards | hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不加战力；source 也在 cleanup removal set 时不入队。 |
| simultaneity guard | same source 同一轮 cleanup pass 中多个友方同时死亡保守封顶为 1 个 trigger；不是 full-official。 |
| compatibility | true stack destruction immediate P79 compatibility 保留，本批不迁移。 |

## 3. Non-Covered FUs

| FU | Reason not covered by 4C-11 |
|---|---|
| `FU-b5cb36a5c9` Viktor | destroyed-unit token family and shared-oracle printings need a separate overlay. |
| `FU-c146331876` Resonant Soul | first-friendly-destroyed-per-turn draw memory needs a separate overlay. |
| `FU-af8b05c294` Kogmaw | AoE last-breath damage can recurse into damage/cleanup. |
| `FU-ee1dfb3ed3` Karthus | global extra last-breath static needs FAQ/rules adjudication. |
| `FU-6a52b04cb2` Undercover Agent | discard/draw last-breath has hidden hand information and choice boundaries. |

## 4. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused RealTriggerQueue tests | 23/23 passed |
| backend full tests | 3364/3364 passed |
| frontend build | passed |
| Chrome smoke | passed |
| Stage 3 preflight | passed |
| diff check | passed |

这些结果由 A 在 B 完成 4C-11 runtime 后报告；E 本轮只落矩阵 / 证据 overlay。

## 5. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch11GhostlyCentaurCleanupTriggerEnqueue`
- `functionalUnits[].stage4C11`，仅用于 `FU-0f2c4a3ea5`
- `fieldDefinitions.functionalUnits.stage4C11`
- `fieldDefinitions.stage4CBatch11GhostlyCentaurCleanupTriggerEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-11 记录

`stage4C11` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。Starfall 只是 supporting source，不因本批升级。

## 6. Counts

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C11` verified FUs | 1 |
| `stage4C11` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 8 |
| cumulative state-based cleanup trigger enqueue verified FUs | 8 |
| next-pressure candidate FUs | 10 |
| full-official upgrades | 0 |

## 7. Verified FU

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-0f2c4a3ea5` | `UNL-068/219` 幽魂半人马 | 4C-11 | `FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / complete trigger engine / temporary power duration cleanup matrix remain. |

## 8. Next Pressure Candidates

这些 FUs / families 只记录为候选，未被 4C-11 标为已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

## 9. Still Missing P0/P1

- 完整 trigger engine。
- Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent 未覆盖。
- same-source 多个友方同时死亡 full-official 裁定。
- true stack destruction queued migration for Ghostly Centaur。
- LayerEngine / temporary power duration cleanup matrix。
- hidden / face-down trigger original visibility modeling；4C-11 只验证 no-enqueue / no-power metadata leak guard。
- FAQ adjudication 与 regression tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。
- 正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**
