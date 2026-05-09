# Stage 4C-7 Scouting Warhawk Trigger Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-7 / E 卡牌覆盖矩阵 overlay**

结论：**4C-7 只部分降低 `OGN·216/298`《侦察飞鹰》的 explicit destroy real trigger enqueue blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `OGN·216/298`《侦察飞鹰》在矩阵中对应 `FU-0500c77a70`，不是猜测。
- `OGN·256/298`《妖异狐火》在矩阵中对应 `FU-a9dc3495e1`，本批只作为 explicit destroy source，不获得 full-official。
- 矩阵规则 / FAQ 证据入口：Scouting Warhawk 现有关联 `JFAQ-251023 p8`；Spirit Fire 现有关联 `CORE-260330 p40`，仍带多项 engine blocker。

## 2. 4C-7 Closed Slice

| 域 | 4C-7 已有证据 |
|---|---|
| verified card | `OGN·216/298`《侦察飞鹰》 |
| verified FU | `FU-0500c77a70` |
| supporting source card | `OGN·256/298`《妖异狐火》 |
| supporting source FU | `FU-a9dc3495e1` |
| registry effect | `SCOUTING_WARHAWK_PLAY_UNIT` |
| trigger effect | `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` |
| supporting effect | `SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4` |
| source event | `UNIT_DESTROYED` |
| runtime path | `UNIT_DESTROYED -> visible Scouting Warhawk SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> RUNES_CALLED` |
| visibility guard | hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`。 |
| compatibility | single-trigger compatibility 保留；没有协议 / 前端变化。 |

## 3. Test Evidence

| 验证项 | 结果 |
|---|---|
| focused RealTriggerQueue tests | 9/9 passed |
| backend full tests | 3350/3350 passed |
| frontend build | passed |
| Chrome smoke | passed |
| Stage 3 preflight | passed |

这些结果由 A 在 B 完成 4C-7 runtime 后报告；E 本轮只落矩阵 / 证据 overlay。

## 4. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch7ScoutingWarhawkTriggerEnqueue`
- `functionalUnits[].stage4C7`，仅用于 `FU-0500c77a70`
- `fieldDefinitions.functionalUnits.stage4C7`
- `fieldDefinitions.stage4CBatch7ScoutingWarhawkTriggerEnqueue`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-7 记录

`stage4C7` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。

## 5. Counts

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C7` verified FUs | 1 |
| `stage4C7` verified snapshot entries | 1 |
| supporting source snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 3 |
| cumulative state-based cleanup trigger enqueue verified FUs | 2 |
| next-pressure candidate FUs | 15 |
| full-official upgrades | 0 |

## 6. Verified FUs

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-0500c77a70` | `OGN·216/298` 侦察飞鹰 | 4C-7 | `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` / complete trigger engine / state-based cleanup path / FAQ adjudication remain. |

`FU-a9dc3495e1` / `OGN·256/298`《妖异狐火》只是 supporting source。它仍保留 4B `IMPLEMENTED_TESTED` + `NEEDS_ENGINE_SUPPORT` 状态，不因 4C-7 获得 full-official。

## 7. Next Pressure Candidates

这些 FUs / families 只记录为候选，未被 4C-7 标为已实现：

- same FU future pressure：`FU-0500c77a70` 的 state-based cleanup enqueue。
- Poro conditional draw：`FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`
- simple state cleanup last-breath：`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

## 8. Still Missing P0/P1

- 完整 trigger engine。
- Scouting Warhawk state-based cleanup enqueue；4C-7 只验证 explicit destroy。
- 其他 last-breath / destroyed / friendly-destroyed functional units。
- hidden / face-down trigger original visibility modeling；4C-7 只验证 no-enqueue / no-rune metadata leak guard。
- FAQ adjudication 与 regression tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。
- 正式 18-step E2E。

是否允许批量 full-official 覆盖：**不允许。**
