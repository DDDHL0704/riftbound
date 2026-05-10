# Stage 4C-16 Mechanical Trickster Trigger Evidence

日期：2026-05-10

结论：**post-freeze evidence overlay only；NOT READY；不授予 full-official。**

## 1. Scope

本文件只记录阶段 4C-16 的卡牌覆盖矩阵 / FAQ evidence overlay。E 不修改服务端、前端、checkpoint、server audit、rules index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

本批只标记：

| 项 | 值 |
|---|---|
| functional unit | `FU-1a392a4ae2` |
| cardNo | `OGN·239/298` |
| card | Mechanical Trickster / 《机械戏法师》 |
| trigger effect kind | `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` |
| overlay field | `functionalUnits[].stage4C16` |
| top-level overlay | `stage4CBatch16MechanicalTricksterTriggerEnqueue` |
| overlay status | `MECHANICAL_TRICKSTER_TRUE_STACK_LAST_BREATH_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |

## 2. Verified Runtime Slice

4C-16 verified path:

`true stack UNIT_DESTROYED -> TRIGGER_QUEUED -> ORDER_TRIGGERS for multi-trigger or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> UNIT_TOKEN_CREATED x3 minions with TOKEN_FAMILY:MINION`

Guard boundary:

- face-down source: no enqueue, no metadata leak, no token.
- standby source: no enqueue, no metadata leak, no token.
- P79 Mechanical Trickster fixture is updated to queue / priority semantics.
- Created minion tokens rely on the 4C-15A `TOKEN_FAMILY:MINION` infrastructure marker.

## 3. Evidence

B/A reported validation:

- `RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack`
- `RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions`
- `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` updated
- backend full passed 3382/3382 by A

The matrix records this as representative runtime evidence only. It is not a full official text / FAQ / all-route proof.

## 4. Matrix Impact

| Metric | Count |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C16` verified FUs | 1 |
| `stage4C16` verified snapshot entries | 1 |
| cumulative real-trigger enqueue verified FUs | 12 |
| cumulative state-based cleanup trigger enqueue verified FUs | 11 |
| full-official upgrades | 0 |

4B `freezeStatus` / `statusFlags` remain unchanged for `FU-1a392a4ae2`; `fullOfficial=false`.

The cleanup trigger enqueue count remains 11 because 4C-16 facts describe only the true stack `UNIT_DESTROYED` route.

## 5. Explicit Non-Coverage

Do not mark these as covered by 4C-16:

- Ironclad Vanguard / `FU-6d0971786b`
- Kogmaw / `FU-af8b05c294`
- Karthus / `FU-ee1dfb3ed3`
- Undercover Agent / `FU-6a52b04cb2`

Still missing:

- Mechanical Trickster state-based cleanup route.
- Complete trigger engine.
- Full trigger-count matrix for multi-source / multi-destroy / simultaneous trigger multiplicity.
- Hidden / face-down original visibility modeling beyond the tested no-enqueue guards.
- FAQ adjudication and regression.
- 1009 snapshot-entry / 811 functional-unit full-official coverage.
- Formal 18-step E2E.

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
