# Stage 4C-37 Berserk Impulse Opponent Top Unit Guard Audit

日期：2026-05-10

结论：**4C-37 representative baseline documented；项目仍 NOT READY。**

本批只关闭 Berserk Impulse / 暴怒冲动 `OGN·025/298` / cardId `31231` / `FU-b05eda44ce` / `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT` 的极窄 opponent top main-deck unit target guard 代表切片。它不升级 full-official，不覆盖 1009/811 全量卡牌，不替代正式 18-step E2E。

## Scope

已完成：

- P1 从手牌打出 Berserk Impulse 并支付 4。
- P1 选择 P2 已揭示 / 代表性 public top main-deck unit。
- 双方 priority pass 后，该单位从 P2 main deck 顶部打出到 P1 base。
- `UNIT_PLAYED_TO_BASE` 记录 source spell、target object、owner P2、played-by P1、source `MAIN_DECK`、destination `BASE`。
- 目标单位 damage reset to 0，until-end-of-turn effects / power modifier 清空，exhausted reset to false。
- invalid target guard 覆盖 friendly top unit、opponent second main-deck unit、top spell / equipment / rune、face-down top unit、private hand / base / battlefield unit。
- invalid target 失败保持 no tick / no events / no payment / no hand movement / no deck movement / no stack item / no unit played / no leak。
- dirty resolution guard 覆盖 top changed、non-unit、face-down、wrong controller / ownership target no move and no `UNIT_PLAYED_TO_BASE`。
- 本批不新增 protocol / frontend shape。

未完成：

- full Berserk Impulse official completion。
- multi-opponent reveal / choose / recycle。
- full hidden-zone prompt / redaction。
- non-unit branch, if any。
- full spell duel / reaction timing。
- full PaymentEngine / LayerEngine / FAQ closure。
- full “play as your own card” owner/controller/payment matrix。
- 1009/811 full-official、正式 18-step E2E。

## Files

服务端：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BerserkImpulseGuardTests.cs`

文档：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_EVIDENCE.md`

## Validation

- Focused backend：17/17 passed。
- D did not rerun tests。
- Backend full / frontend build / Chrome smoke are not recorded for this D docs pass.

## P0 / P1

4C-37 已关闭：

- Berserk Impulse opponent top main-deck unit target guard representative route。
- Berserk Impulse invalid target no-mutation/no-leak guard representative route。
- Dirty resolution no-move guard representative route。
- Owner/source/play-by event semantics representative check。

仍阻断 READY：

- Berserk Impulse full hidden-zone reveal / choose / recycle remains P0 / design-gated for final READY。
- 1009 snapshot entries / 811 functional units full-official coverage。
- completion audit、正式 18-step E2E。
- 完整 PaymentEngine / LayerEngine / FEPR / hidden-information / target-invalidation matrices。
- multi-opponent reveal / choose / recycle、non-unit branch、hidden-zone prompt / redaction、spell duel / reaction timing、private-zone replay redaction。

口径：**NOT READY；`fullOfficial=false`；不允许 READY-CANDIDATE。**
