# 阶段 4C-14 Savage Jawfish Trigger Enqueue 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-14 Savage Jawfish trigger enqueue baseline 的规则证据与 P0/P1 审计口径。D 本轮只更新用户授权的 checkpoint / audit docs，不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。

## 1. 4C-14 关闭的 P0 子项

4C-14 新增一个 FU 的 real trigger enqueue baseline：

- Savage Jawfish / 《凶残颚鱼》（`CATALOG` UNL-129/219，`FU-bd94334cc5`）。
- true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 均进入 `TriggerQueue`。
- 多触发走 `ORDER_TRIGGERS`；单触发 auto-stack。
- priority 双方 pass 后 `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1。
- Guard：source 必须仍在场、face-up、non-standby、同 controller，不能是被摧毁对象 / cleanup removal set。
- hidden face-down / standby / opponent-controlled source 不 enqueue、不泄漏、不加经验。
- 旧 P79 Savage Jawfish fixture 已更新为 queue / stack / priority semantics。

4C-14 明确边界：

- 同一来源同一 cleanup / stack pass 中多个友方被摧毁时，当前最小切片保守 cap 为每 source 每 pass 最多一次。
- 该 cap 不是 full official trigger-count matrix；必须作为 P1 / TODO 保留。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-14 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| Stack destruction `UNIT_DESTROYED` 触发入队 | `CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | true stack `UNIT_DESTROYED` 可触发 Savage Jawfish 入队，之后走 `ORDER_TRIGGERS` 或 single-trigger auto-stack、`StackItems`、priority pass 结算 | 完整 stack destruction trigger engine、更多 destroyed-family FU、FAQ regression |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | Starfall lethal state-based cleanup `UNIT_DESTROYED` 可触发 Savage Jawfish 入队 | 替代 / 预防、repeat-until-stable、更多 cleanup 来源和 FAQ 回归 |
| Savage Jawfish experience +1 | `CATALOG` UNL-129/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；experience 相关官方页码 TODO | priority 双方 pass 后 `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1 | 完整 experience rules、更多经验来源、完整 effect resolution |
| Source legality / visibility guard | `CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码 TODO | source 必须仍在场、face-up、non-standby、同 controller，不能是 destroyed object / cleanup removal set；hidden face-down / standby / opponent source 不入队、不泄漏、不加经验 | face-down 原始触发建模、显露窗口、viewer 级 trigger metadata 全路径、控制权变化组合 |
| Per-source per-pass cap | 当前工程保守边界；精确官方触发次数证据 TODO | 同一来源同一 cleanup / stack pass 多个友方被摧毁时，当前最小切片每 source 每 pass 最多一次 | full official trigger-count matrix、同一事件多对象触发次数、APNAP 与 cleanup 多轮交织 |

## 3. A 复核记录

- Focused：`RealTriggerQueueTests|P79SavageJawfish` 通过，33/33。
- Backend full：passed，3374/3374。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

## 4. 仍保留 P0/P1

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup / stack pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：Savage Jawfish 同一来源同一 pass 多个友方被摧毁时的 full official trigger-count matrix。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 5. D 审计结论

4C-14 可关闭 Savage Jawfish / `FU-bd94334cc5` 的 trigger enqueue baseline：true stack destruction 与 Starfall lethal state-based cleanup 两条 `UNIT_DESTROYED` 代表路径均进入真实触发队列，并经排序 / 入栈 / 优先权结算为 `EXPERIENCE_GAINED` +1。

项目仍 **NOT READY**：本批新增一个 FU 的代表性 real enqueue 证据，但不覆盖 full official trigger-count matrix、Viktor / Kogmaw / Karthus / Undercover Agent、完整 trigger engine、hidden / face-down 原始触发建模、完整 effect resolution、FAQ regression、1009 / 811 full-official 或最终正式 18-step E2E。阶段 4C 仍处于逐 FU、逐测试的小批推进中，`fullOfficialUpgrades=0`。
