# 阶段 4 主控任务

日期：2026-05-09
当前结论：**NOT READY**

阶段 4 目标是把阶段 3 的最小可运行核心流程扩展为最终候选前的完整交付面：1009 条 2026-04-27 官网固定快照卡牌映射、811 个 functional units 覆盖、FAQ / 规则回归、后端 full test、前端 build、Chrome smoke、正式 18 步 E2E 与 completion audit。

阶段 4 可以由 A 在 4A 到 4H 的内部门禁间自动推进；任一停机条件触发时必须停止、输出 checkpoint，并等待用户确认。阶段 4 不允许自动 `update_goal complete`，不允许自动标记最终 READY。只有所有 P0/P1 清零、测试全绿、审计通过后，才允许输出 **READY-CANDIDATE**，且仍需等待用户确认。

## 总原则

- 服务端是唯一规则权威。
- 前端只展示 authoritative snapshot。
- 前端只提交服务端 ActionPrompt / prompt candidate 支持的操作。
- 前端不得自行裁决支付、伤害分配、触发排序、战场控制、战斗结果、法术对决结果、得分、胜负或对手隐藏信息。
- 验收范围固定为 `data/official` 中 2026-04-27 官网快照，不实时抓取官网新数据改变阶段 4 口径。

## 内部门禁

### 4A：第三阶段基线复核

复核 3D checkpoint、后端 full test、前端 build、Chrome smoke、`PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS`、battlefield / standby / control / conquer / cleanup / battle / damage 基线，以及隐藏信息不泄漏。失败则停住，不进入 4B。

### 4B：卡牌覆盖矩阵冻结

冻结统计口径：snapshot entries = 1009、functional units = 811、cardId、collectorId、oracle/effectId 或等价 functional unit、token / rune / battlefield / promo / 异画计入口径。更新 cardId -> functional unit、functional unit -> implementation / evidence / tests / status 矩阵，输出未覆盖列表、top risk list 和批量实现顺序。失败则停住，不进入 4C。

### 4C：高风险 functional units 批量实现与测试

按 functional unit / oracle/effectId 实现，不按卡号盲目实现。同文本 / 异画 / 同功能卡可以复用 effect implementation，但 cardId 映射必须完整。优先处理 PAY_COST、ASSIGN_COMBAT_DAMAGE、ORDER_TRIGGERS、battlefield / control / conquer、standby、cleanup、spell duel、battle、replacement、prevention / prohibition、continuous effects / layers、hidden information、deck / rune deck / discard / recycle / burn out。每批必须更新矩阵、补服务端测试、运行相关测试并保持 full test 不回退。

### 4D：FAQ / 规则证据回归

按优先级引用仓库内五份规则 / FAQ PDF：最新核心规则、官方 FAQ / 勘误、系列官方 FAQ、裁判 FAQ、开发文档。为卡组构筑、开局、起手、回合、召符文、抽牌 / 燃尽、主阶段、结算链、priority、focus、spell duel、trigger ordering、payment、replacement / prohibition / continuous effects、layers、move / recall、battlefield control、standby、battle、damage assignment、battle cleanup、scoring / victory、hidden information 建 evidence 和 regression tests。

### 4E：前端产品级补齐与隐藏信息复审

收口首页、大厅、图鉴、卡组、房间、起手、对战桌面、战场、手牌、结算链、法术对决、支付、伤害分配、触发排序、日志、得分、胜负 / 投降、设置和错误提示。前端继续只消费服务端 snapshot / prompt，SnapshotDebugPanel 与对手视角不得泄漏手牌、牌堆顺序、面朝下待命真实信息、隐藏随机结果或 hidden metadata。

### 4F：全量测试

运行后端 full test、前端 build、Chrome smoke、卡牌效果覆盖测试、FAQ regression tests、hidden information tests、已有 deterministic / replay / recovery tests，并记录全部命令和结果。任一失败则停住，不进入 4G。

### 4G：正式 18 步 E2E

执行正式双浏览器 18 步 E2E，不得用 smoke 或 preflight 冒充。必须覆盖创建 / 加入房间、选择合法标准构筑卡组、准备、开始、起手、第一回合、召符文、抽牌、打出合法单位或法术、移动单位到战场、争夺 / 结算链 / 法术对决、双方让过或提交服务端合法提示、状态不分叉、至少一处战场得分或控制权变化、进入下一回合并可投降或正常胜负结算。还必须检查隐藏信息、服务端 / 前端状态一致、无前端规则裁决，以及断线重连 smoke 或明确 P0/P1 判断。

### 4H：completion audit / READY 候选

生成 `docs/CURRENT_COMPLETION_AUDIT.md`，覆盖修改 / 新增文件、服务端规则补齐、前端页面、接口契约、卡牌覆盖矩阵、1009 snapshot entries、811 functional units、FAQ evidence、hidden information、后端 full test、前端 build、Chrome smoke、正式 18 步 E2E、P0/P1 清零、剩余 P2 和最终结论。存在任何 P0/P1 时最终结论必须是 **NOT READY**。

## 停机条件

出现以下任一情况，A 必须停止推进并等待用户确认：

- 后端 full test 失败。
- 前端 build 失败。
- Chrome smoke 失败且不是明确环境问题。
- 正式 18 步 E2E 失败。
- hidden information 泄漏。
- 前端出现本地规则裁决路径。
- 覆盖矩阵无法解释 1009 / 811 差异。
- 官方规则 / FAQ 冲突且无法自动决定。
- 新增 P0。
- 子 agent 写入锁冲突。
- 需要改变协议核心字段。
- 需要用户决定规则解释。
- 无法保证 deterministic / replay / recovery。
- 准备输出 READY-CANDIDATE 前。

## 分工

- B：服务端规则、effect implementation、protocol runtime、测试。
- C：前端页面、UI、prompt 交互、Chrome smoke、E2E UI 支持。
- D：文档、规则证据、P0/P1 审计、completion audit。
- E：卡牌覆盖矩阵、official text / FAQ evidence、functional units、测试优先级。
- A：checkpoint、写入锁、diff 审查、测试审查、evidence 审查和阻断汇总；不亲自大规模实现代码，除非用户明确授权。

## Git 策略

- 每个内部门禁通过、测试全绿、无 P0/P1 新增时，阶段 4 允许自动 checkpoint commit。
- 不得提交 `riftbound-dotnet.sln`。
- 不得提交临时日志或无关本地文件。
- 不得 merge。
- 不得自动 `update_goal complete`。
