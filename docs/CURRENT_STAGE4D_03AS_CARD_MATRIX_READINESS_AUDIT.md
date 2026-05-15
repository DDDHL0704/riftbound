# Stage 4D-03AS Azir Optional Armament Reattach Card Matrix Readiness Audit

日期：2026-05-15
结论：**READINESS IMPROVED / MATRIX NOT UPDATED / PROJECT NOT READY**

本文件是 A/E 侧对 4D-03AS Azir optional armament reattach follow-up 的 card matrix readiness 审计。它只读取官方快照、4D-03AM evidence 与现有矩阵骨架，不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不把任何状态升级为 full-official。

## 1. Official Snapshot Facts

`data/official/card-catalog.zh-CN.json` 固定快照中存在两个 Azir 条目：

| cardId | cardNo | name | category | color | energy | power | text |
|---:|---|---|---|---|---:|---:|---|
| 33126 | `SFD·050/221` | 阿兹尔 | 英雄单位 | green | 6 | 6 | `支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。` |
| 33127 | `SFD·050a/221` | 阿兹尔 | 英雄单位 | green | 6 | 6 | same official text |

Both entries share one functional unit and must preserve exact collector coverage.

## 2. Current Matrix State

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` currently maps both collector entries to:

- `functionalUnitId`: `FU-105abedc17`
- `functionalRepresentativeNo`: `SFD·050/221`
- `functionalUnitSize`: 2
- `officialTextHash`: `634fdf9096546a6917a88cf73d210281184d10c8`
- `implementationStatus`: `direct-card-behavior`
- `implementedEffectKinds`: `SFD_050_AZIR_SWAP_MOVE_SKILL_PLAY_UNIT`, `SFD_050A_AZIR_SWAP_MOVE_SKILL_PLAY_UNIT`
- `stage4B.freezeStatus`: `SHARED_ORACLE_IMPLEMENTATION`
- `stage4B.statusFlags`: `IMPLEMENTED_UNTESTED`, `SHARED_ORACLE_IMPLEMENTATION`, `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW`
- `stage4B.fullOfficial`: `false`
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW`, `NEEDS_AUTOMATED_TEST_EVIDENCE`
- `stage4B.automatedTests.status`: `NO_FU_LEVEL_AUTOMATED_TEST_IN_MATRIX`

Current dependency domains:

- `FEPR/Targeting/TimingWindows`
- `LayerEngine/ContinuousEffects`
- `PaymentEngine/PAY_COST`
- `ZoneOwnership/ControlChange/Movement`

## 3. Evidence Limit Before 4D-03AS

4D-03AM evidence proves the green typed payment, controlled-unit target, once-per-turn marker, ordinary stack item and precise position swap representative. It explicitly leaves optional armament reattach deferred.

Therefore current evidence may support a narrower unarmed / no-reattach representative, but it cannot support full official Azir while the official optional armament sentence remains unimplemented.

## 4. 4D-03AS Runtime Evidence

`docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md` and `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_EVIDENCE.md` now record accepted runtime / focused-test evidence for the optional armament reattach branch:

- prompt metadata now reports `armamentReattachPolicy=implemented`
- target-scoped armament choices are exposed server-side
- command accepts no choice or one legal target-attached armament choice
- invalid hand-written choices reject no-mutation
- stack resolution rechecks selected armament and emits `EQUIPMENT_REATTACHED` only when still legal
- stale selected armament skips reattach without blocking the legal location swap

A-side verification passed focused 204/204, adjacent 397/397, backend full 4355/4355 and `git diff --check`.

This improves readiness for `FU-105abedc17`, but the matrix JSON remains unchanged in this batch.

## 5. Matrix Update Gate After 4D-03AS

After this 4D-03AS implementation, E/A may consider a matrix update only if all of the following are true:

1. A opens a separate matrix write window after accepting implementation diff and tests for prompt, command, server-side optional selection, stack-time revalidation, equipment state update and event audit.
2. Automated evidence covers both exact collector ids or a shared-oracle mapping that explicitly names both `SFD·050/221` and `SFD·050a/221`.
3. The optional reattach branch covers no-choice, one-choice, multiple attached armaments, stale chosen armament and invalid hand-written choice cases.
4. FAQ references currently recorded for this FU are reviewed for equipment / attachment / timing interactions.
5. Broader swift timing / reaction-speed policy is either accepted as the current representative boundary or separately implemented and tested.

If future matrix review finds the optional armament branch incomplete or only partially tested, this FU must remain `fullOfficial=false`.

## 6. Current Verdict

4D-03AS removes the optional armament reattach blocker for the accepted Azir representative and improves readiness for a future matrix write window. No matrix JSON update is allowed in this batch, `FU-105abedc17` remains `fullOfficial=false`, and the project remains **NOT READY**.
