# Stage 4C-35 Vengeance Destroy Target Guard Audit

日期：2026-05-10

结论：**4C-35 representative baseline complete；项目仍 NOT READY。**

本批只关闭 Vengeance / 复仇 `OGN·229/298` / cardId `31467` / `FU-07104fa58a` / `VENGEANCE_DESTROY_UNIT` 的极窄 public unit destroy target guard 代表切片。它不升级 full-official，不覆盖 1009/811 全量卡牌，不替代正式 18-step E2E。

## Scope

已完成：

- P1 从手牌打出 Vengeance，选择合法 public unit target。
- 双方 priority pass 后，目标进入 owner graveyard。
- 目标从 base / battlefield zone 与 `CardObjects` public state 移除。
- 合法目标覆盖 friendly / enemy public unit targets in base / battlefield。
- invalid target guard 覆盖 stale unit、face-down standby、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit。
- invalid target 失败保持 no tick / no events / no payment / no hand movement / no stack item / no destroy / no leak。
- face-down standby 与 private-zone target 不暴露真实身份。

未完成：

- full Vengeance official completion。
- 完整 destroy / cleanup / replacement / prevention / Last Breath interaction。
- attached-equipment detach / replacement breadth。
- destroyed-this-turn memory breadth。
- Spellshield target tax、target invalidation、完整 FEPR target matrix。
- all destroy-target spell family、1009/811 full-official、正式 18-step E2E。

## Files

服务端：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/VengeanceDestroyGuardTests.cs`

文档 / 矩阵：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_EVIDENCE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

## Validation

- Focused backend：`Vengeance|DestroyGuard|Destroy` 通过 107/107。
- Adjacent guard regression：`HuntTheWeak|Vengeance|DestroyGuard` 通过 23/23。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3506/3506。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `git diff --check` 通过。

## P0 / P1

4C-35 已关闭：

- Vengeance public unit destroy target representative route。
- Vengeance invalid target no-mutation/no-leak guard representative route。

仍阻断 READY：

- 1009 snapshot entries / 811 functional units full-official coverage。
- completion audit、正式 18-step E2E。
- 完整 PaymentEngine / FEPR / replacement / prevention / cleanup / Last Breath / hidden-information / target-invalidation matrices。

口径：**NOT READY；`fullOfficial=false`；不允许 READY-CANDIDATE。**
