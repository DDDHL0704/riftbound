# 阶段 4C-16 Mechanical Trickster Trigger 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-16 Mechanical Trickster / 《机械戏法师》last-breath create minions 迁移为真实 TriggerQueue / Stack / Priority 语义的规则证据与 P0/P1 审计口径。B 本批修改服务端规则实现与 conformance 测试；D 本轮只更新文档，不修改服务端、前端、E matrix 或 `riftbound-dotnet.sln`。

## 1. 范围

- 批次：4C-16 Mechanical Trickster trigger enqueue baseline。
- 卡牌：Mechanical Trickster / 《机械戏法师》 / `OGN·239/298`。
- effect kind：`MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- 代码改动事实：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`。
- 本批选择 safe route：迁移既有 Mechanical Trickster immediate token create，不进入 Ironclad Vanguard、Kogmaw、Karthus、Undercover。

## 2. 关闭的代表性子项

4C-16 已关闭 Mechanical Trickster 旧 immediate token create -> real trigger queue 的代表性迁移：

- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack。
- 多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3。
- face-down / standby Mechanical Trickster 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` fixture 已更新为 queue / priority semantics。

新增 / 更新测试：

- `RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack`
- `RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions`
- `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` updated

## 3. 规则证据入口

- Mechanical Trickster card text / effect：`CATALOG` `OGN·239/298`；effect kind `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- Stack destruction 与触发入队：`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 333-340；p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Token creation representative：`CORE-260330` p39-p42 rules 355-356；具体 Mechanical Trickster token 细节以 `CATALOG` / token factory evidence 为准。
- Hidden / face-down / standby visibility guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码仍为 TODO，不编造。

## 4. 验证记录

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3382/3382。
- A frontend build / smoke：将在 D 文档后由 A 继续运行；本 D 文档不把前端验证提前记为完成。

## 5. 仍保留 P0/P1

- P1：Ironclad Vanguard 仍是旧 immediate compatibility，未迁移到 real trigger queue。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks 未覆盖。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合仍缺。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径仍缺。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E 仍缺。

4C-16 只关闭 Mechanical Trickster 代表性迁移，不宣称 Ironclad Vanguard / Kogmaw / Karthus / Undercover 已覆盖，不宣称 full trigger engine，不宣称 READY / READY-CANDIDATE。
