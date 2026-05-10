# 阶段 4C-15B Viktor Trigger 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-15B Viktor destroyed non-minion token trigger 最小官方化代表切片的规则证据与 P0/P1 审计口径。B 本批只修改服务端规则实现与 conformance 测试；D 本轮只更新文档，不修改服务端、前端、E matrix 或 `riftbound-dotnet.sln`。

前置 checkpoint：`034f1ed checkpoint: complete stage 4C minion token family baseline`。

## 1. 范围

- 批次：4C-15B Viktor destroyed non-minion trigger baseline。
- 目标 FU：`FU-b5cb36a5c9`。
- 覆盖卡号：`ARC-006/006`、`OGN·246/298`、`OGN·246a/298`。
- 代码改动事实：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`。
- 4C-15A 已先补 `TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily` 前置模型；4C-15B 在此基础上实现代表性 trigger baseline。

## 2. 关闭的代表性子项

4C-15B 已关闭 Viktor `FU-b5cb36a5c9` 的最小官方化代表路径：

- visible surviving friendly Viktor source 看到另一名友方非随从单位被摧毁时触发。
- destroyed target 使用 pre-removal `CardObjectState` 判定：是 unit、与 source 同 controller / friendly、不是 source、且没有 `CardObjectTags.MinionTokenFamily`。
- source guard：Viktor 必须仍在场、face-up、non-standby、同 controller，且不在 cleanup removal set 中。
- 覆盖 true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED`。
- trigger path：`TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED`，在 controller base 创建 1 战力 Zaun minion `OGN·273/298`，并带 `TOKEN_FAMILY:MINION`。
- Guard：minion target 不入队、不造 token；hidden / face-down / standby / opponent source 不入队、不泄漏、不造 token；source 同时死亡不入队。

新增 / 覆盖测试：

- `RealViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken`
- `StateBasedCleanupViktorDestroyedNonMinionTriggersAutoStackAndCreatesMinionToken`
- `ViktorDestroyedMinionTargetDoesNotEnqueueTrigger`
- `StateBasedCleanupInvalidViktorSourcesDoNotEnqueueOrLeak`
- `StateBasedCleanupViktorSkipsWhenSourceAlsoDies`

## 3. 规则证据入口

- Viktor card text / FU：`CATALOG` `ARC-006/006`、`OGN·246/298`、`OGN·246a/298`；`FU-b5cb36a5c9`。
- Stack destruction 与触发入队：`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 333-340；p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Non-minion filter 前置模型：4C-15A `TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`；官方三种“随从”token factory `OGN·271/298`、`OGN·272/298`、`OGN·273/298`。
- Hidden / face-down / standby visibility guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码仍为 TODO，不编造。

## 4. 验证记录

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3380/3380。
- B `git diff --check`：pending / expected passed；A 将在文档后再次复核。

## 5. 仍保留 P0/P1

- P1：same source same stack / cleanup pass multiple non-minion friendly deaths 的 full official trigger-count matrix 仍采取保守 one source once，未官方化。
- P0：Kogmaw / Karthus / Undercover Agent 等其他 destroyed-family / friendly-destroyed FUs 未覆盖。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择 / 完整 APNAP 组合仍缺。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径仍缺。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E 仍缺。

4C-15B 只代表性关闭 Viktor destroyed non-minion trigger baseline，不实现 Kogmaw / Karthus / Undercover，不宣称 full trigger engine，不宣称 READY / READY-CANDIDATE。
