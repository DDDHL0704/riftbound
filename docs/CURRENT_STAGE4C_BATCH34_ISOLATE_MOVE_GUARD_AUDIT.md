# Stage 4C-34 Isolate Move Guard Audit

日期：2026-05-10

状态：**4C-34 representative guard slice complete；项目仍 NOT READY；`fullOfficial=false`。**

本批只覆盖 Isolate / 隔绝 `UNL-124/219` / cardId `34667` / `FU-175d573ae4` / `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW` 的极窄 enemy public battlefield unit move-to-owner-base no-draw 代表路径与 target guard hardening。

## 已关闭代表子项

- P1 打出 Isolate，选择合法 enemy public battlefield unit target，双方 priority pass 后结算，目标移动到 owner base。
- 代表路径确认不产生 `CARD_DRAWN`，本 fixture 锁定 no-draw 分支。
- 移动后保留目标 damage / power / exhausted / object identity。
- 服务端在 `PLAY_CARD` validation 中拦截 invalid Isolate targets，不依赖前端判断。
- friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均返回 `INVALID_TARGET`。
- invalid target 失败命令 no tick / no events / no payment / no hand movement / no stack item / no move / no draw。
- face-down standby target 被拒绝且不暴露真实身份，opponent hidden info 继续受 viewer-specific snapshot / redaction 保护。

## 修改文件

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/IsolateMoveToBaseGuardTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_EVIDENCE.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `docs/CURRENT_CARD_EFFECT_RISK_TOP20.md`
- `docs/CURRENT_STAGE4B_CARD_COVERAGE_FREEZE.md`

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Isolate|FullyQualifiedName~MoveToBase|FullyQualifiedName~MoveGuard"` 通过 46/46。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~IsolateMoveToBaseGuardTests|FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 48/48。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3495/3495。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过。

## 未关闭项

- Isolate 的落单敌方单位抽牌分支仍未官方化。
- 完整 movement / roam / precise battlefield model 仍未完成。
- 完整 FEPR target selection / target-change matrix 仍未完成。
- 完整 ZoneOwnership / ControlChange / Movement matrix 仍未完成。
- 完整 hidden / face-down / standby target visibility model 仍未完成。
- Spellshield target tax、replacement / prevention / cleanup 交织仍未完成。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated。
- 1009 entries / 811 functional units full-official 覆盖、FAQ regression、正式 18-step E2E 和 completion audit 仍未完成。

## 结论

4C-34 可以作为 Isolate no-draw move-to-owner-base target guard representative baseline 记录，但不能升级为 full-official，也不能作为 READY / READY-CANDIDATE 依据。
