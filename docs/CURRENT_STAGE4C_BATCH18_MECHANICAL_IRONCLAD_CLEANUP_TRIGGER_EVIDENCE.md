# Stage 4C-18 Mechanical + Ironclad Cleanup Trigger Evidence

日期：2026-05-10

结论：**post-freeze evidence overlay only；NOT READY；不授予 full-official。**

## 1. Scope

本文件只记录阶段 4C-18 的卡牌覆盖矩阵 / FAQ evidence overlay。E 不修改服务端、测试、前端、一般审计文档、rules index 或 `riftbound-dotnet.sln`，不进入 1009 张卡 full-official 实现。

本批只标记：

| FU | cardNo | card | trigger effect kind |
|---|---|---|---|
| `FU-1a392a4ae2` | `OGN·239/298` | Mechanical Trickster / 《机械戏法师》 | `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` |
| `FU-6d0971786b` | `SFD·021/221` | Ironclad Vanguard / 《铁甲先锋》 | `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` |

Top-level overlay：`stage4CBatch18MechanicalIroncladCleanupTriggerEnqueue`。

Per-FU overlay：`functionalUnits[].stage4C18`。

Overlay status：`STATE_BASED_CLEANUP_LAST_BREATH_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。

## 2. Verified Cleanup Route

4C-18 records this representative route:

`state-based cleanup LETHAL_DAMAGE / UNIT_DESTROYED -> last-breath trigger queued -> TRIGGER_QUEUED -> ORDER_TRIGGERS for multi-trigger or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED`

Results:

- Mechanical Trickster: `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION`.
- Ironclad Vanguard: `UNIT_TOKEN_CREATED x2` robots.

Automated evidence:

- `StateBasedCleanupMechanicalTrickstersTriggerOrderAndCreateMinionsThroughStack`
- `StateBasedCleanupIroncladVanguardsTriggerOrderAndCreateRobotsThroughStack`
- `StateBasedCleanupHiddenMechanicalTrickstersDoNotEnqueueTriggers`
- `StateBasedCleanupHiddenIroncladVanguardsDoNotEnqueueTriggers`
- backend full: 3388/3388 passed by A
- frontend build: passed by A
- Chrome smoke: passed by A

Prior true-stack coverage remains recorded separately:

- Mechanical Trickster true stack route: 4C-16.
- Ironclad Vanguard true stack route: 4C-17.

## 3. Matrix Impact

| Metric | Count |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C18` verified FUs | 2 |
| `stage4C18` verified snapshot entries | 2 |
| cumulative real-trigger enqueue verified FUs | 13 |
| cumulative state-based cleanup trigger enqueue verified FUs | 13 |
| full-official upgrades | 0 |
| full-official still uncovered FUs | 811 |

4B `freezeStatus` / `statusFlags` remain unchanged for both FUs; `fullOfficial=false`.

## 4. Explicit Non-Coverage

Do not mark these as covered by 4C-18:

- Kogmaw / `FU-af8b05c294`
- Karthus / `FU-ee1dfb3ed3`
- Undercover Agent / `FU-6a52b04cb2`

Still missing:

- Complete trigger engine beyond these representative cleanup baselines.
- Multi-source / multi-destroy / simultaneous trigger multiplicity.
- Hidden / face-down original visibility modeling beyond tested guards.
- FAQ adjudication and regression.
- 1009 snapshot-entry / 811 functional-unit full-official coverage.
- Formal 18-step E2E.

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
