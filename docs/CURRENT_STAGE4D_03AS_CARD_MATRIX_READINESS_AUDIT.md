# Stage 4D-03AS Azir Optional Armament Reattach Card Matrix Readiness Audit

日期：2026-05-15
结论：**READINESS AUDIT ONLY / MATRIX NOT UPDATED / PROJECT NOT READY**

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

## 4. Matrix Update Gate After 4D-03AS

After a future 4D-03AS implementation exists, E/A may consider a matrix update only if all of the following are true:

1. A accepts implementation diff and tests for prompt, command, server-side optional selection, stack-time revalidation, equipment state update and event audit.
2. Automated evidence covers both exact collector ids or a shared-oracle mapping that explicitly names both `SFD·050/221` and `SFD·050a/221`.
3. The optional reattach branch covers no-choice, one-choice, multiple attached armaments, stale chosen armament and invalid hand-written choice cases.
4. FAQ references currently recorded for this FU are reviewed for equipment / attachment / timing interactions.
5. A opens a separate matrix write window; implementation work must not edit the matrix.

If optional armament reattach remains unimplemented or only partially tested, this FU must remain `fullOfficial=false`.

## 5. Current Verdict

4D-03AS identifies the next Azir matrix blocker after 4D-03AM. It is ready for a future narrow runtime slice, but no runtime write lock is open in this paused batch. No matrix JSON update is allowed, and the project remains **NOT READY**.
