# Stage 4C-15B Viktor Trigger Evidence

更新时间：2026-05-10

结论：**NOT READY；4C-15B 只关闭 Viktor destroyed non-Minion token trigger 的最小代表性 baseline，不授予 full-official，不允许 1009/811 批量实现。**

## Source Boundary

本文件只记录 E 覆盖矩阵 / FAQ evidence / functional unit 侧事实。E 不修改功能代码、前端、D checkpoint、server audit、rules index 或 `riftbound-dotnet.sln`。

官方数据仍使用 `data/official` 中 2026-04-27 固定快照：

- frozen snapshot entries：1009
- frozen functional units：811
- cardId / collector no / oracle effectId 口径不变

## 4C-15B Closed Representative Baseline

4C-15B 覆盖一个 FU：

| FU | CardNos | Result | 4C-15B status |
|---|---|---|---|
| `FU-b5cb36a5c9` | `ARC-006/006`, `OGN·246/298`, `OGN·246a/298` | `UNIT_TOKEN_CREATED` 1-power Zaun Minion `OGN·273/298` with `TOKEN_FAMILY:MINION` | `VIKTOR_DESTROYED_NON_MINION_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |

官方文本功能摘要：Viktor 在场时，你的另一名非“随从”单位被摧毁后，在基地打出一名 1 战力随从。

覆盖路径：

- `true stack UNIT_DESTROYED -> TriggerQueue -> single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> UNIT_TOKEN_CREATED 1-power Zaun minion OGN·273/298 with TOKEN_FAMILY:MINION`
- `Starfall lethal cleanup UNIT_DESTROYED -> TriggerQueue -> single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> UNIT_TOKEN_CREATED 1-power Zaun minion OGN·273/298 with TOKEN_FAMILY:MINION`

## Filters And Guards

Destroyed target pre-removal filter：

- must be unit
- must be same controller / friendly
- must not be source
- must not have `CardObjectTags.MinionTokenFamily`

Source guard：

- source must be field
- source must be face-up
- source must be non-standby
- source must be same controller
- source must not be in removal set

No-enqueue guards：

- destroyed Minion target
- hidden / face-down / standby / opponent-controlled source
- source also dying

## Dependency On 4C-15A

4C-15B depends on the 4C-15A Minion token family marker:

- `TOKEN_FAMILY:MINION`
- `CardObjectTags.MinionTokenFamily`
- output token factory：`OGN·273/298` / `FU-77e07d2cad`

## Non-Covered FUs

4C-15B 不覆盖：

- `FU-af8b05c294` Kogmaw
- `FU-ee1dfb3ed3` Karthus
- `FU-6a52b04cb2` Undercover Agent

## Test Evidence

A/B 报告的验证结果：

- new RealTriggerQueueTests：5
- backend full：3380/3380 passed

## Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层：`stage4CBatch15BViktorTriggerEnqueue`
- per-FU：`functionalUnits[].stage4C15B`

仅 `FU-b5cb36a5c9` 有 `stage4C15B`。

## Counts

- frozen snapshot entries：1009
- frozen functional units：811
- `stage4C15B` verified FUs：1
- `stage4C15B` verified snapshot entries：3
- cumulative real trigger enqueue verified FUs：11
- cumulative state-based cleanup trigger enqueue verified FUs：11
- fullOfficialUpgrades：0

## Still Missing P0/P1

- full official trigger-count matrix for Viktor
- complete trigger engine beyond representative Viktor baseline
- multi-source / multi-destroy / simultaneous trigger multiplicity
- hidden / face-down original visibility modeling beyond tested guards
- Kogmaw / Karthus / Undercover Agent
- FAQ adjudication and regression tests
- 1009 snapshot-entry / 811 functional-unit full-official coverage
- formal 18-step E2E

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
