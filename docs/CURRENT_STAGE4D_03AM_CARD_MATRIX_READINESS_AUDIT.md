# Stage 4D-03AM Azir Card Matrix Readiness Audit

日期：2026-05-15
结论：**READINESS AUDIT ONLY / MATRIX NOT UPDATED / PROJECT NOT READY**

本文件是 A/E 侧对 4D-03AM Azir green swift swap 切片的 card matrix readiness 审计。它只读取官方快照、规则证据索引和现有矩阵骨架，不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不把任何状态升级为 full-official。

## 1. 官方快照事实

`data/official/card-catalog.zh-CN.json` 固定快照中存在两个 Azir 条目：

| cardId | cardNo | name | category | color | energy | power | text |
|---:|---|---|---|---|---:|---:|---|
| 33126 | `SFD·050/221` | 阿兹尔 | 英雄单位 | green | 6 | 6 | `支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。` |
| 33127 | `SFD·050a/221` | 阿兹尔 | 英雄单位 | green | 6 | 6 | same official text |

Both entries share the same official behavior text and should remain mapped to one functional unit while preserving exact collector ids.

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

## 3. Existing Evidence Limit

`docs/rules-evidence-index.md` contains static ordinary-play entries:

- `p2-preflight-play-sfd-050-azir-swap-skill-static`
- `p2-preflight-play-sfd-050a-azir-swap-skill-static`

Those entries only prove ordinary hand-play static behavior: base 6 cost, no targets on play, stack pass-pass, battlefield/base placement as a 6-power Bird hero unit. They explicitly defer the green swift position-swap ability, original-position memory, and optional armament reattach path.

Therefore the current matrix cannot use those static play evidence rows to claim the official activated ability is implemented.

## 4. Matrix Update Gate After B

After 4D-03AM-B runtime implementation exists, E/A may consider a matrix update only if all of the following are true:

1. A accepts B diff and tests for prompt, command, payment, target validation, no-mutation rollback, server-authoritative position swap, event / snapshot update and once-per-turn guard.
2. The matrix receives concrete automated test evidence for both exact collector ids or a shared oracle mapping that explicitly covers both `SFD·050/221` and `SFD·050a/221`.
3. FAQ references currently recorded as `SOUL-JFAQ-260114 p12`, `p21`, and `p25` are reviewed for Azir-specific or rule-domain interactions.
4. Optional armament reattach is either implemented and tested or recorded as a residual blocker.
5. A opens a separate matrix write window; B implementation must not edit the matrix.

If optional armament reattach remains unimplemented, this FU may receive narrower representative evidence for the unarmed-target swap path, but must remain `fullOfficial=false`.

## 5. Current Verdict

4D-03AM is matrix-ready as a targeted runtime slice because the exact collector ids, functional unit and blockers are already known. It is not matrix-complete. No matrix JSON update is allowed in this read-only audit batch, and the project remains **NOT READY**.
