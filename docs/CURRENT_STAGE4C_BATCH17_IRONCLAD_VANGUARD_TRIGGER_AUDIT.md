# 阶段 4C-17 Ironclad Vanguard Trigger 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录阶段 4C-17 Ironclad Vanguard / 《铁甲先锋》last-breath create robots 迁移到真实 TriggerQueue / Stack / Priority 语义的规则证据与 P0/P1 审计口径。B 已完成服务端实现与测试，A 已复跑后端 full test、前端 build、Chrome smoke、矩阵 JSON/断言与 diff check。整体仍 **NOT READY**。

## 1. 范围

- 批次：4C-17 Ironclad Vanguard trigger enqueue baseline。
- 卡牌：Ironclad Vanguard / 《铁甲先锋》 / `SFD·021/221`。
- 候选 effect kind：`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- 当前目标：把旧 immediate last-breath create robots 路径迁移为真实 `TriggerQueue` / `StackItems` / priority 代表路径。
- 本批不进入 Kogmaw、Karthus、Undercover，不宣称 full trigger engine。
- 覆盖矩阵：使用冻结矩阵中 `SFD·021/221` 的现有 `FU-6d0971786b`；`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 作为 4C-17 overlay `triggerEffectKind` 记录。

## 2. 已关闭代表项

以下为 4C-17 已验证的代表性子项：

- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack。
- 多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x2，创建两名 3 战力 Robot / 机器人 token 到 controller base。
- face-down / standby Ironclad Vanguard 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed` fixture 已更新为 queue / priority semantics。

## 3. 规则证据入口

- Ironclad Vanguard card text / effect：`CATALOG` `SFD·021/221`；effect kind `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- Stack destruction 与触发入队：`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 333-340；p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Token creation representative：`CORE-260330` p39-p42 rules 355-356；具体 Robot token 细节以 `CATALOG` / token factory evidence 为准。
- Hidden / face-down / standby visibility guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码仍为 TODO，不编造。

## 4. 验证结果

- B focused filter：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79IroncladVanguard"` 通过 42/42。
- B backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3384/3384。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3384/3384。
- A frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- A Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-17 matrix assertions 通过。

## 5. 仍保留 P0/P1

- 已关闭 P1 子项：Ironclad Vanguard true stack destruction 旧 immediate migration 已迁移到 real trigger queue / stack / priority 代表路径。
- 仍留 P1：Ironclad Vanguard state-based cleanup last-breath route 未在本批官方化。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks 未覆盖。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合仍缺。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径仍缺。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E 仍缺。

4C-17 只关闭 Ironclad Vanguard true stack 代表性 migration，不代表 full-official，不代表 18-step E2E，不代表 READY / READY-CANDIDATE。
