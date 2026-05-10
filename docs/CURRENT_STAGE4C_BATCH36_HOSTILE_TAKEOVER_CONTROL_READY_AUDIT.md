# Stage 4C-36 Hostile Takeover Control Ready Guard Audit

日期：2026-05-10

结论：**4C-36 representative baseline documented；项目仍 NOT READY。**

本批只关闭 Hostile Takeover / 恶意收购 `SFD·202/221` / cardId `33301` / `FU-00ee09c2cc` / `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` 的极窄 enemy public battlefield unit gain-control + ready target guard 代表切片。它不升级 full-official，不覆盖 1009/811 全量卡牌，不替代正式 18-step E2E。

## Scope

已完成：

- P1 从手牌打出 Hostile Takeover，选择 enemy public battlefield unit。
- 双方 priority pass 后，P1 获得该单位控制权并 ready。
- 目标 owner remains P2，controller becomes P1，object remains battlefield。
- 代表路径安排 `RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2`。
- 既有 P5 end-turn return / recall fixture 可作为临时控制归还并召回 owner base 的代表证据。
- invalid target guard 覆盖 friendly battlefield unit、enemy base unit、stale object、face-down standby、battlefield equipment、battlefield spell object、battlefield rune object、hand / private unit。
- invalid target 失败保持 no tick / no events / no payment / no hand movement / no stack item / no control / no ready / no leak。
- face-down standby 与 private-zone target 不暴露真实身份。
- 本批不新增 protocol / frontend shape。

未完成：

- full Hostile Takeover official completion。
- 待命 / reaction timing。
- battle-start / conquer branch。
- 完整 battlefield / control-zone lifecycle。
- 完整 owner/controller matrix。
- 完整 end-turn cleanup task model。
- Spellshield target tax、target invalidation、完整 FEPR target matrix。
- all control-change spell family、1009/811 full-official、正式 18-step E2E。

## Files

服务端：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/HostileTakeoverGuardTests.cs`

文档：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_EVIDENCE.md`

## Validation

- Focused backend：通过 265/265。
- Adjacent guard regression：通过 157/157。
- Backend full：通过 3515/3515。
- Frontend build：通过。
- Chrome smoke：通过。

## P0 / P1

4C-36 已关闭：

- Hostile Takeover enemy public battlefield unit gain-control + ready representative route。
- Hostile Takeover invalid target no-mutation/no-leak guard representative route。
- Owner/controller split and end-turn return scheduling representative checks。

仍阻断 READY：

- 1009 snapshot entries / 811 functional units full-official coverage。
- completion audit、正式 18-step E2E。
- 完整 PaymentEngine / FEPR / replacement / prevention / cleanup / hidden-information / target-invalidation matrices。
- Hostile Takeover full standby / reaction timing、battle-start / conquer branch、control-zone lifecycle、owner/controller matrix、end-turn cleanup task model。

口径：**NOT READY；`fullOfficial=false`；不允许 READY-CANDIDATE。**
