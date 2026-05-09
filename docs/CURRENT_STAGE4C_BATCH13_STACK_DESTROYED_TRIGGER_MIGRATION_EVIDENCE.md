# Stage 4C-13 Stack Destroyed Trigger Migration Evidence

更新时间：2026-05-10

结论：**NOT READY；4C-13 只是 route migration evidence overlay，不新增 unique FU coverage，不授予 full-official，不允许 1009/811 批量实现。**

## Source Boundary

本文件只记录 E 覆盖矩阵 / FAQ evidence / functional unit 侧事实。E 不修改服务端、前端、A checkpoint、server audit、rules index 或 `riftbound-dotnet.sln`。

官方数据仍使用 `data/official` 中 2026-04-27 固定快照：

- frozen snapshot entries：1009
- frozen functional units：811
- cardId / collector no / oracle effectId 口径不变

## 4C-13 Closed Route Migration

4C-13 将两个已覆盖 FU 的 true stack destruction non-cleanup `UNIT_DESTROYED` 路径从旧 P79 immediate compatibility 迁移到 queue / priority 语义：

`true stack destruction non-cleanup UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> effect result`

| FU | Card | Route result | Prior cleanup overlay | 4C-13 status |
|---|---|---|---|---|
| `FU-0f2c4a3ea5` | `UNL-068/219`《幽魂半人马》 | `POWER_MODIFIED_UNTIL_END_OF_TURN +2` | `stage4C11` | `TRUE_STACK_DESTRUCTION_TRIGGER_QUEUE_MIGRATED_NOT_FULL_OFFICIAL` |
| `FU-c146331876` | `OGN·118/298`《残响之魂》 | `CARD_DRAWN 1` | `stage4C12` | `TRUE_STACK_DESTRUCTION_TRIGGER_QUEUE_MIGRATED_NOT_FULL_OFFICIAL` |

4C-13 是 route migration，不是新增 unique FU coverage：

- `stage4C13RouteUpgradedFunctionalUnits = 2`
- cumulative real trigger enqueue verified FUs 保持 9
- cumulative state-based cleanup trigger enqueue verified FUs 保持 9
- fullOfficialUpgrades 保持 0

## Guards

- cleanup path 仍由 4C-11 / 4C-12 覆盖。
- cleanup path 已从 old stack helper 排除，避免 duplicate enqueue。
- old P79 immediate compatibility 已移除 / 迁移；P79 现在断言 queue / priority semantics。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不生效。
- same-source / same-owner multiple simultaneous deaths full matrix 仍未覆盖。

## Non-Covered FUs

4C-13 不覆盖：

- `FU-b5cb36a5c9` Viktor destroyed-unit token family
- `FU-af8b05c294` Kogmaw AoE last-breath damage
- `FU-ee1dfb3ed3` Karthus global last-breath static
- `FU-6a52b04cb2` Undercover Agent discard/draw last-breath

这些 FU 仍只能保留为 next-pressure candidates。

## Test Evidence

A/B 报告的验证结果：

- focused RealTriggerQueue：30/30 passed
- backend full：3370/3370 passed
- frontend build：passed
- Chrome smoke：passed
- Stage 3 preflight：passed
- diff check：passed

## Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层：`stage4CBatch13StackDestroyedTriggerMigration`
- per-FU：`functionalUnits[].stage4C13`

仅以下两个 FU 有 `stage4C13`：

- `FU-0f2c4a3ea5`
- `FU-c146331876`

## Still Missing P0/P1

- complete trigger engine beyond migrated Ghostly / Resonant true-stack routes and prior visible cleanup slices
- Viktor / Kogmaw / Karthus / Undercover Agent
- same-source / same-owner multiple simultaneous deaths full-official adjudication
- per-turn destroyed owner memory full reset matrix beyond tested Resonant route
- hidden / face-down trigger original visibility modeling beyond tested no-enqueue guards
- FAQ adjudication and regression tests
- 1009 snapshot-entry / 811 functional-unit full-official coverage
- formal 18-step E2E

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
