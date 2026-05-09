# 阶段 3A 计划：Smoke 基线 / 强类型复杂命令 / PAY_COST 最小 runtime / 对战桌面外壳

更新日期：2026-05-09
当前 HEAD：`4b41e81`
结论：**NOT READY**

本文是阶段 3A 的当前执行口径。`docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 保留为宽阶段 3 总审计图，但当前不展开完整阶段 3，只收口 3A 的四个小目标。

## 1. 3A 范围

3A 只收口：

- Smoke 基线：建立可重复运行的 Chrome smoke，并记录运行命令、覆盖范围、失败/通过证据和隐藏信息断言。
- 强类型复杂命令解析：`PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 不再只靠 raw `UnsupportedCommand` shell 进入 Core；`GameCommandJsonMapper` / Hub / recovery path 需要能映射为对应 typed command。
- `PAY_COST` 最小 runtime 切片：只做最小可用 pending payment window、payload 校验、服务端合法选择、提交、拒绝/失败语义和零副作用；不要求完整 PaymentEngine 全覆盖。
- 对战桌面外壳安全接线：前端可展示房间 / match / status / prompt / snapshot / safe fallback，但不得裁决规则、不得自行构造复杂选择、不得泄漏隐藏信息。

3A 明确不进入：

- 最终正式 18 步 E2E。
- 1009 张卡 full-official 覆盖与全量实现。
- 完整 battle runtime、damage assignment runtime、ORDER_TRIGGERS runtime。
- 完整 battlefield / standby / control / held / conquer lifecycle。
- 完整 PaymentEngine、LayerEngine、replay determinism 全路径审计。

## 2. 当前 3A P0

| P0 | 规则 / 契约依据 | 当前实现状态 | 归属 agent | 3A 下一步 |
| --- | --- | --- | --- | --- |
| 3A-P0-001 Chrome smoke 基线 | A 目标文档阶段 3 / Chrome smoke；`CORE-260330` rules 107-129 隐藏信息边界 | 已关闭：`npm run smoke:chrome -- --start-api` 可启动 API / DevUi / Chrome headless-CDP，并验证 7 个基础路由无脚本捕获 runtime error | C 主实现；A 验收；D 记录 | 后续 3B 再扩成双人连续流程 smoke；不把本 smoke 等同最终 18 步 E2E |
| 3A-P0-002 三类复杂命令强类型映射 | `CommandTypes`、`PayCostCommand`、`AssignCombatDamageCommand`、`OrderTriggersCommand`、`ActionPromptContracts` | 已关闭：`GameCommandJsonMapper` 映射三类 complex command；malformed payload 有稳定拒绝；raw/unsupported 兼容保留 | B 主实现；D 审计 | 后续只在对应 runtime 开放时扩合法性，不回退为前端裁决 |
| 3A-P0-003 `PAY_COST` 最小 runtime | `CORE-260330` rules 131、135.2.e、162-167、356-357、377、403-405、414、416；`JFAQ-251023` q2.5 | 已关闭最小切片：服务端 pending payment prompt 暴露 `paymentId/paymentWindow/paymentChoiceIds`，合法支付可关闭窗口，失败/过期不改权威状态 | B 主实现；E 补最小支付 fixture；C 只展示服务端 prompt；D 审计 | 后续仍需完整 PaymentEngine、decline、替代/额外费用与所有非出牌支付窗口 |
| 3A-P0-004 前端对战桌面外壳不得裁决规则 | 服务端权威原则；`CORE-260330` rules 107-129；`ActionPromptDto` / `SnapshotDto` 契约 | 已关闭 3A 外壳：桌面只读 authoritative snapshot / prompt candidate；`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 与未知 complex prompt safe fallback | C 主实现；B 提供契约；D 审计 | 后续正式复杂交互必须等待服务端 runtime schema 冻结 |

## 3. 当前 3A P1

| P1 | 当前风险 | 归属 agent | 下一步 |
| --- | --- | --- | --- |
| 3A-P1-001 宽阶段 3 文档容易被误读为当前验收范围 | `CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 保留了完整阶段 3 总图；如果不加 3A 口径，B/C/E 可能误把 3B/3C 内容拉入本轮 | D | 已新增本文；后续所有阶段 3A 报告优先引用本文 |
| 3A-P1-002 smoke 证据格式未统一 | 已有 3A 命令、路由覆盖与 console/runtime error 捕获；尚不是双窗口隐藏信息或最终 18 步 E2E 证据 | C/D/A | 3B 起每条 smoke 继续记录命令、commit、覆盖点、console/network、hidden-info、仍缺口 |
| 3A-P1-003 `PAY_COST` 最小 runtime 与完整 PaymentEngine 边界易混 | 最小 runtime 只能证明一个支付窗口切片，不能关闭替代费用、触发费用、拒付全族、资源技能全路径 | B/D/E | 每次记录必须写“关闭子项”和“仍缺口”，避免误称 READY |

## 4. B/C 完成后的证据位

| 证据位 | 交付对象 | 需要填写 | D 审计状态 |
| --- | --- | --- | --- |
| 3A-EVID-C-SMOKE | Chrome smoke 基线 | 命令、commit、浏览器上下文数量、覆盖步骤、console/network 结果、隐藏信息断言、失败截图或日志 | VERIFIED by A：`npm run smoke:chrome -- --start-api` 通过；不是最终 18 步 E2E |
| 3A-EVID-B-MAPPER | 强类型复杂命令解析 | `GameCommandJsonMapper` 映射状态、Hub path 测试、recovery path 测试、malformed payload 错误码、raw fallback 兼容说明 | VERIFIED by A：后端 full test 3324/3324 通过 |
| 3A-EVID-B-PAYCOST | `PAY_COST` 最小 runtime | 规则依据、prompt 字段、command payload、成功/失败/decline 或拒付语义、零副作用测试、仍缺 PaymentEngine 范围 | VERIFIED by A：最小 runtime 测试和 Hub stale/invalid/legal path 已通过；完整 PaymentEngine 仍缺 |
| 3A-EVID-C-SHELL | 对战桌面外壳安全接线 | 页面/组件范围、只读 snapshot 字段、可提交 action 来源、复杂 prompt fallback、隐藏信息 DOM/store/WS 断言 | VERIFIED by A：build 与 Chrome smoke 通过；后续双窗口隐藏信息断言仍属于 3B/最终 E2E |

## 5. 3A 验收红线

- 不得把 smoke 通过等同于正式 18 步 E2E 通过。
- 不得把 `PAY_COST` 最小 runtime 等同于完整 PaymentEngine。
- 不得把三类复杂命令 typed mapper 等同于 damage/order/battle runtime 完成。
- 不得让前端根据卡面、catalog、本地状态或 metadata 自行裁决费用、目标、伤害、触发排序、战场控制、胜负。
- A 已完成阶段 3A 验收，但项目整体继续保持 **NOT READY**。
