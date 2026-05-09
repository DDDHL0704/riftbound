# Stage 4C-14 Savage Jawfish Trigger Evidence

更新时间：2026-05-10

结论：**NOT READY；4C-14 只是 Savage Jawfish friendly-destroyed trigger enqueue evidence overlay，不授予 full-official，不允许 1009/811 批量实现。**

## Source Boundary

本文件只记录 E 覆盖矩阵 / FAQ evidence / functional unit 侧事实。E 不修改服务端、前端、A checkpoint、server audit、rules index 或 `riftbound-dotnet.sln`。

官方数据仍使用 `data/official` 中 2026-04-27 固定快照：

- frozen snapshot entries：1009
- frozen functional units：811
- cardId / collector no / oracle effectId 口径不变

## 4C-14 Closed Slice

4C-14 覆盖一个新增 FU：

| FU | Card | Trigger result | 4C-14 status |
|---|---|---|---|
| `FU-bd94334cc5` | `UNL-129/219` Savage Jawfish / 《凶残颚鱼》 | `EXPERIENCE_GAINED +1` | `FRIENDLY_DESTROYED_STACK_AND_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |

官方文本功能摘要：当另一名友方单位被摧毁时，凶残颚鱼获得 1 经验。

覆盖路径：

- `true stack UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EXPERIENCE_GAINED +1`
- `Starfall lethal cleanup UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EXPERIENCE_GAINED +1`

## Guards

- source must remain field, face-up, non-standby, same controller, and not destroyed/removal set。
- hidden face-down / standby / opponent-controlled source 不入队、不泄漏、不获得经验。
- same source same pass multi-destroy trigger multiplicity 仍为 P1/TODO，不是 full-official。

## Non-Covered FUs

4C-14 不覆盖：

- `FU-b5cb36a5c9` Viktor destroyed-unit token family
- `FU-af8b05c294` Kogmaw AoE last-breath damage
- `FU-ee1dfb3ed3` Karthus global last-breath static
- `FU-6a52b04cb2` Undercover Agent discard/draw last-breath

这些 FU 仍只能保留为 next-pressure candidates。

## Test Evidence

A/B 报告的验证结果：

- focused RealTriggerQueue：33/33 passed
- backend full：3374/3374 passed
- frontend build：passed
- Chrome smoke：passed
- Stage 3 preflight：passed
- diff check：passed

## Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层：`stage4CBatch14SavageJawfishTriggerEnqueue`
- per-FU：`functionalUnits[].stage4C14`

仅 `FU-bd94334cc5` 有 `stage4C14`。

## Counts

- `stage4C14` verified FUs：1
- `stage4C14` verified snapshot entries：1
- cumulative real trigger enqueue verified FUs：10
- cumulative state-based cleanup trigger enqueue verified FUs：10
- fullOfficialUpgrades：0
- frozen snapshot entries：1009
- frozen functional units：811

## Still Missing P0/P1

- complete trigger engine beyond visible verified slices
- same source same pass multi-destroy trigger multiplicity P1/TODO
- Viktor / Kogmaw / Karthus / Undercover Agent
- hidden / face-down trigger original visibility modeling beyond tested no-enqueue guards
- FAQ adjudication and regression tests
- 1009 snapshot-entry / 811 functional-unit full-official coverage
- formal 18-step E2E

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
