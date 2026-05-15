# Stage 4D-03AP Rek'Sai Card Matrix Readiness Audit

日期：2026-05-15
结论：**READINESS AUDIT ONLY / MATRIX NOT UPDATED / PROJECT NOT READY**

本文件是 A/E 侧对 4D-03AP Rek'Sai HASTE_READY red exactness 切片的 card matrix readiness 审计。它只读取官方快照、规则证据索引和现有矩阵骨架，不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不把任何状态升级为 full-official。

2026-05-15 post-test addendum：4D-03AP focused guard 已由 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_EVIDENCE.md` 验收，focused 17/17、handoff focused 126/126、adjacent 442/442、backend full 4338/4338、`git diff --check` 通过。该证据可作为未来 matrix write window 的 HASTE_READY typed-red exactness representative 输入，但由于 strong / Overwhelm battle modifier、damage overflow、non-hand friendly haste granting、LayerEngine 与 FAQ breadth 仍 residual，本文件仍不更新 matrix JSON，不声明 full-official，项目仍 **NOT READY**。

## 1. 官方快照事实

`data/official/card-catalog.zh-CN.json` 固定快照中存在：

| cardId | cardNo | name | category | energy | power | text |
|---:|---|---|---|---:|---:|---|
| 33104 | `SFD·029/221` | 雷克塞 | 英雄单位 | 3 | 3 | `{{急速}}` extra `{{1}}` + `{{红色}}` ready-entry / `{{强攻}}` / non-hand friendly unit gains `{{急速}}` |
| 33105 | `SFD·029a/221` | 雷克塞 | 英雄单位 | 3 | 3 | same oracle text |

## 2. Current Matrix State

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` currently maps this collector family to:

- `functionalUnitId`: `FU-1945f6918c`
- `functionalRepresentativeNo`: `SFD·029/221`
- `functionalUnitSize`: 2
- `implementedEffectKinds`: `REKSAI_PLAY_UNIT_NO_OPTIONAL_HASTE_OVERWHELM`, `REKSAI_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE_OVERWHELM`
- `stage4B.freezeStatus`: `IMPLEMENTED_TESTED`
- `stage4B.statusFlags`: `IMPLEMENTED_TESTED`, `SHARED_ORACLE_IMPLEMENTATION`, `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW`
- `stage4B.fullOfficial`: `false`
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW`
- `faqRefs`: `BREAK-JFAQ-260416 p3`, `SOUL-JFAQ-260114 p19`, `SOUL-OFAQ-260114 p4`

Current dependency domains include PaymentEngine / PAY_COST, battle / damage assignment, LayerEngine, hidden information and FAQ adjudication.

## 3. Existing Evidence Limit

`docs/rules-evidence-index.md` contains 4C-52 ordinary no-optional play-unit + keyword tag guard evidence and old P4 HASTE_READY fixture rows for both Rek'Sai printings.

Those rows still explicitly defer:

- red resource exactness.
- strong / Overwhelm battle modifier.
- `ASSIGN_COMBAT_DAMAGE` overflow behavior.
- non-hand friendly unit gains haste.
- LayerEngine, hidden-info, FAQ refs, 1009/811 full-official and formal 18-step E2E.

Therefore the current matrix cannot use 4C-52 evidence alone to claim full official Rek'Sai. 4D-03AP can only provide narrower typed-red HASTE_READY representative evidence.

## 4. Matrix Update Gate After Focused Evidence

After 4D-03AP-B implementation / test guard and A-side evidence, E/A may consider a future matrix write window only if all of the following are true:

1. A accepts focused evidence for both `SFD·029/221` and `SFD·029a/221`, or one representative plus explicit alias coverage.
2. Prompt and command evidence proves HASTE_READY requires red typed power, not generic power or wrong-trait power.
3. Payment-resource evidence proves necessary red rune recycle is quoted and command accepted, while wrong-trait / duplicate / invalid / unnecessary recycle rejects no-mutation.
4. Residual official text is explicitly retained: strong battle modifier, damage overflow, non-hand friendly haste granting, LayerEngine, FAQ and hidden-info breadth.
5. A opens a separate matrix write window; implementation must not edit the matrix.

If any official text remains unimplemented, this FU may receive narrower representative evidence for red-pay HASTE_READY exactness, but must remain `fullOfficial=false`.

## 5. Current Verdict

4D-03AP is matrix-ready as a targeted evidence / guard slice because the collector ids, functional unit, existing evidence and blockers are known. It is not matrix-complete. No matrix JSON update is allowed in this read-only audit batch, and the project remains **NOT READY**.
