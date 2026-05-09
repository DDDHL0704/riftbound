# 阶段 4C-6 Honest Broker Cleanup Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-6 Honest Broker state-based cleanup trigger enqueue 最小切片的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵或 `riftbound-dotnet.sln`。

## 1. 4C-6 关闭的 P0 子项

4C-6 可以关闭以下 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221），不扩展到隐藏源或待命源。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两个 Honest Broker，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。
- hidden / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏隐藏或待命来源。
- 本批不改协议或前端，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-6 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | `OGN·029/298` 伤害导致的 `LETHAL_DAMAGE` cleanup 可触发可见 Honest Broker 绝念入队 | 替代 / 预防、repeat-until-stable、其他 cleanup 来源和更多 destroyed-family |
| Honest Broker last-breath enqueue | `CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 两个可见 Honest Broker 同时被 state-based cleanup 摧毁后进入 `TriggerQueue`、`ORDER_TRIGGERS`、`StackItems` 并创建金币装备 token | 其他 last-breath / friendly-destroyed functional units 与完整 trigger engine |
| Hidden / standby source redaction | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | hidden / standby Honest Broker 不从该切片入队，也不创建 token，避免 prompt metadata 泄漏不可见来源 | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径 |
| Starfall lethal cleanup representative | `CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356 | Starfall 作为致命伤害来源提供 state-based cleanup 入口，不代表全部伤害 / 清理 / 触发组合完成 | FAQ regression 与更多法术 / 技能 / 战斗伤害来源 |

## 3. A 验证记录

- Focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~RealTriggerQueueTests` 通过，6/6。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3348/3348。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过，仅有既有 SignalR / Rollup `PURE` 注释警告。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过。
- B 文件 diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

## 5. D 审计结论

4C-6 可作为 `ORDER_TRIGGERS` / trigger engine / cleanup queue 的第六个阶段性关闭点：它证明 state-based cleanup `LETHAL_DAMAGE` 可把可见 Honest Broker last-breath 代表路径送入真实触发队列并经排序 / 入栈 / 优先权结算创建金币装备 token。

项目仍 **NOT READY**：本批不覆盖完整 trigger engine、其他 destroyed-family、hidden / face-down 原始触发建模、FAQ regression、1009 / 811 full-official 或最终正式 18 步 E2E。
