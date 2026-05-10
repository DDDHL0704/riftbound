# 阶段 4C-19 Kogmaw Last-Breath AoE 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对 Kogmaw / 克格莫 `OGN·190/298` / `FU-af8b05c294` 绝念 AoE damage 代表切片的文档、规则证据与 P0/P1 审计口径。A 已验证实现、focused/backend full/frontend build/Chrome smoke/diff/矩阵断言；本页只代表 4C-19 representative baseline，不代表 full-official。

## 范围

- 只覆盖 Kogmaw visible、face-up、field source 的 last-breath AoE damage representative baseline。
- 已验证路径：`UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> battlefield units take 4 damage -> cleanup queue stabilizes。
- AoE 使用 source pre-removal battlefield location，只伤害该 battlefield 的当前单位；其他 battlefield 单位不受伤害。
- hidden / face-down / standby Kogmaw source 不入队、不泄漏 prompt metadata、不造成 AoE damage。
- Kogmaw 被摧毁但缺少 battlefield location 时安全降级为 no-enqueue / no-damage。
- 不实现 Karthus 额外绝念。
- 不实现 Undercover Agent discard / draw。
- 不实现 full trigger engine、full-official、1009 / 811 全量或正式 18-step E2E。

## 规则证据入口

- `CATALOG` `OGN·190/298`：Kogmaw / 克格莫官方卡牌文本与 FU `FU-af8b05c294`。
- `CORE-260330` p4-p8 rules 107-129：可见性、对象与隐藏信息边界。
- `CORE-260330` p14-p15 rules 142-143：战力、伤害与伤害后的对象状态。
- `CORE-260330` p31-p35 rules 318-340：清理、任务 / 结算链与 priority。
- `CORE-260330` p52-p55 rules 383.3.d-383.3.e：触发技能与多触发排序。
- `CORE-260330` p77 rule 460：战斗 / 伤害后状态清理相关入口。
- FAQ 精确页码：`JFAQ-251023 p7` 作为矩阵 evidence candidate；仍需后续 FAQ adjudication / regression。

## 已验证证据

- 服务端实现：`src/Riftbound.Engine/CoreRuleEngine.cs` 增加 Kogmaw last-breath trigger queue / stack / priority / AoE damage resolution 代表路径。
- 服务端测试：`tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 增加 Kogmaw true stack、state-based cleanup、hidden / standby guard、missing battlefield location safe-degrade 代表测试。
- Focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests&FullyQualifiedName~Kogmaw"` 通过 4/4。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3392/3392。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Hygiene：`git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-19 matrix assertions 通过。
- 代表测试名：`RealKogmawLastBreathDealsFourToDestroyedBattlefieldAndCleanupStabilizes`、`StateBasedCleanupKogmawLastBreathDealsFourToDestroyedBattlefield`、`StateBasedCleanupHiddenKogmawsDoNotEnqueueOrDealAoeDamage`、`RealKogmawDestroyedWithoutBattlefieldLocationDoesNotEnqueueOrDealDamage`。
- 协议 / 前端字段：本批无变更。

## 仍保留 P0/P1

- P0：Karthus、Undercover Agent 仍未实现；Kogmaw 本批也只计划代表性 baseline。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P1：same source same pass / simultaneous destruction / AoE damage 后多轮 cleanup 与触发交织的 full official multiplicity matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 entries / 811 functional units full-official 覆盖。
- P0：正式 18-step E2E 与 completion audit。

4C-19 当前判断：**Kogmaw AoE last-breath representative baseline 已验证，但不能关闭 READY 阻断**。
