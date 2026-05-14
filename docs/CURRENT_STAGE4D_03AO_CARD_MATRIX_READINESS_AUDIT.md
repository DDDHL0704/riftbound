# Stage 4D-03AO Ezreal Card Matrix Readiness Audit

日期：2026-05-15
结论：**READINESS AUDIT ONLY / MATRIX NOT UPDATED / PROJECT NOT READY**

本文件是 A/E 侧对 4D-03AO Ezreal blue swift move-to-base 切片的 card matrix readiness 审计。它只读取官方快照、规则证据索引和现有矩阵骨架，不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不把任何状态升级为 full-official。

## 1. 官方快照事实

`data/official/card-catalog.zh-CN.json` 固定快照中存在：

| cardId | cardNo | name | category | energy | power | text |
|---:|---|---|---|---:|---:|---|
| 33162 | `SFD·082/221` | 伊泽瑞尔 | 英雄单位 | 4 | 3 | `当我进攻或防守时，对此处的一名敌方单位造成等同于我战力的伤害。` / `我无法造成战斗伤害。` / `支付{{蓝色}}：{{迅捷}} — 将我移动到你的基地。` |
| 33163 | `SFD·082a/221` | 伊泽瑞尔 | 英雄单位 | 4 | 3 | same oracle text |
| 33164 | `SFD·082b/221·P` | 伊泽瑞尔 | 英雄单位 | 4 | 3 | same oracle text |

## 2. Current Matrix State

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` currently maps this collector family to:

- `functionalUnitId`: `FU-2dca1ad450`
- `functionalRepresentativeNo`: `SFD·082/221`
- `functionalUnitSize`: 3
- `officialTextHash`: `38321c50bcf6cd2e17e448b91c903fc910828350`
- `implementationStatus`: `direct-card-behavior`
- `implementedEffectKinds`: `SFD_082_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT`, `SFD_082A_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT`, `SFD_082B_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT`
- `stage4B.freezeStatus`: `IMPLEMENTED_TESTED`
- `stage4B.statusFlags`: `IMPLEMENTED_TESTED`, `SHARED_ORACLE_IMPLEMENTATION`, `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW`
- `stage4B.fullOfficial`: `false`
- `stage4B.automatedTests.status`: `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`

Current dependency domains include:

- `BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE`
- `FEPR/Targeting/TimingWindows`
- `LayerEngine/ContinuousEffects`
- `PaymentEngine/PAY_COST`
- `ZoneOwnership/ControlChange/Movement`

## 3. Existing Evidence Limit

`docs/rules-evidence-index.md` contains the three `p2-preflight-play-sfd-082...ezreal-combat-damage-static` rows. They prove only ordinary hand-play entry into base as a 3-power `CARD_TYPE:UNIT` hero unit, plus invalid play-input guard coverage.

Those rows explicitly defer:

- attack / defense trigger damage equal to Ezreal's power.
- "I cannot deal combat damage" static behavior.
- blue payment / swift move-to-base activated ability.
- movement / control-zone matrix, LayerEngine, swift timing, FAQ adjudication and full-card official coverage.

Therefore the current matrix cannot use 4C-49 ordinary play evidence to claim the official blue swift activated ability is implemented.

## 4. Matrix Update Gate After Runtime

After 4D-03AO-B runtime implementation and A-side evidence, E/A may consider a future matrix write window only if all of the following are true:

1. A accepts B diff and tests for prompt, command, blue payment, source validation, no-target command guard, server-authoritative move-to-base, event / snapshot update and stale no-effect behavior.
2. Concrete automated test evidence is recorded for all three Ezreal collector Nos or for one representative with explicit alias coverage.
3. Attack / defense trigger and cannot-combat-damage static are either implemented and tested or explicitly retained as battle / LayerEngine residuals.
4. Full swift / reaction timing breadth is either implemented and tested or explicitly retained as a timing residual.
5. A opens a separate matrix write window; B implementation must not edit the matrix.

If any official text remains unimplemented, this FU may receive narrower representative evidence for blue-pay self movement, but must remain `fullOfficial=false`.

## 5. Current Verdict

4D-03AO is matrix-ready as a targeted runtime slice because the collector ids, functional unit, existing evidence and blockers are known. It is not matrix-complete. No matrix JSON update is allowed in this read-only audit batch, and the project remains **NOT READY**.
