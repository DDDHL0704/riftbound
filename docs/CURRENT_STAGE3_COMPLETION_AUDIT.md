# 阶段 3D / 第三阶段收口审计

日期：2026-05-09
当前基线 checkpoint：`2c10a1b`
结论：**NOT READY**

本文是 D 文档 / 规则证据 / P0-P1 审计 agent 对第三阶段的收口判断。D 只修改文档；3D 阶段的 B/C/E 已并行补齐 `ORDER_TRIGGERS` 最小 runtime / UI / evidence。本文不启动最终验收版 18 步 E2E，不进入 1009 张卡全量实现，不标记 READY。

## 1. 3D 范围

3D D 文档只收口：

- 汇总 3A / 3B / 3C 的关闭子项、仍缺口和 superseded 口径。
- 对齐 B/C/E 已完成的 `ORDER_TRIGGERS` / 多触发排序最小 runtime、UI 与 evidence，并记录仍缺口。
- 审计 priority/focus、spell duel close、battle lifecycle、damage assignment、battle cleanup、battlefield control update、conquer/hold scoring、standby visibility、cleanup queue 的证据状态。
- 判断是否允许进入阶段 4，并明确阶段 4 与最终验收边界。

3D 不进入：

- 最终正式 18 步 E2E。
- 1009 张卡 full-official 覆盖或全量实现。
- 由 D 新增服务端 runtime、前端 runtime、测试或卡牌矩阵实现。
- git commit；最终 checkpoint 由 A 汇总。

## 2. 第三阶段收口判断

| 阶段 | 关闭内容 | 未关闭内容 | D 收口判断 |
| --- | --- | --- | --- |
| 3A | Chrome route smoke 基线；三类复杂命令 typed mapper；`PAY_COST` 最小 runtime；前端对战桌面外壳不裁决规则 | 完整 PaymentEngine、`DECLINE_PAY_COST`、`ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` runtime、完整 battle / spell duel、最终 18 步 E2E | 3A 当前最小切片已关闭；不能外推为 Stage 3 完成或 READY |
| 3B | battlefield / standby snapshot 只读字段；非法待命 cleanup 代表路径；control / held / conquer 代表结果；central cleanup queue 最小 task view | cleanup queue 全触发面、control freeze/release、delayed illegal standby 全时机、held/conquer scoring order、全战场卡 | 3B 最小官方化切片已关闭；完整 battlefield lifecycle 仍阻断 READY |
| 3C | spell duel focus/pass/close 代表链；battle view / battle resolution 最小 task；`ASSIGN_COMBAT_DAMAGE` 最小 runtime prompt / submit / reject / simultaneous commit；battle damage -> cleanup -> control update 代表链 | 完整 `SPELL_DUEL_ACTION`、完整 battle task、完整 damage assignment 全规则矩阵、battle cleanup 全路径、control freeze/release、替代/预防 / LayerEngine | 3C 最小切片已关闭；完整 battle / spell duel / damage / cleanup 仍阻断 READY |
| 3D | `ORDER_TRIGGERS` 最小 runtime window / UI / evidence；本文、server audit、frontend gaps、rule evidence todo、checkpoint 的第三阶段收口审计和阶段 4 入口建议 | 未关闭完整 trigger engine、完整 effect resolution、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment | A final validation 已通过；第三阶段可判定 DONE，项目仍 **NOT READY** |

## 3. 规则域证据状态

| 规则域 | 规则 / FAQ 入口 | 第三阶段证据状态 | 仍缺口 | 下一阶段 |
| --- | --- | --- | --- | --- |
| priority / focus | `CORE-260330` rules 307-313、333-348；`JFAQ-251023` q3.1-q3.3 | `PASS_PRIORITY`、`PASS_FOCUS`、spell duel focus、prompt stamp、stale prompt 代表证据已有 | 完整 `SPELL_DUEL_ACTION`、所有反应 / 迅捷 / 反制链、触发排序与 focus 交织 | 阶段 4 |
| spell duel close | `CORE-260330` rules 333-348；`JFAQ-251023` q3.1-q3.3 | 3C 已有 close -> damage assignment -> cleanup/control update 代表链；旧 `p2-preflight-spell-duel-pass-focus-closes-window` 已确认存在 | 所有 close -> next task 全路径、非战斗法术对决、触发排序和替代/预防交织 | 阶段 4 |
| battle lifecycle | `CORE-260330` rules 454-461；rules 319-324；`JFAQ-251023` q2.3-q2.4 | `BattleState`、`BattleResolutionState`、多攻防代表路径、3C damage assignment 最小 runtime 已有 | 完整 battle task、初始战斗结算链、响应窗口、全部多攻防组合、freeze/release | 阶段 4 |
| damage assignment | `CORE-260330` rules 142-143、417、460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 3C 已关闭最小 prompt / validation / submit / reject / simultaneous commit | 壁垒、后排、同优先级、负战力、不可分配、替代/预防全矩阵 | 阶段 4 |
| battle cleanup | `CORE-260330` rules 319-324、461-464；`JFAQ-251023` q5.1-q5.2 | 3C 已有 battle damage -> lethal cleanup -> battle close -> battlefield control update 代表链 | cleanup queue 全触发面、替代/预防、LayerEngine、control freeze/release 全路径 | 阶段 4 |
| battlefield control update | `CORE-260330` rules 187-189、344-348、461-464；`JFAQ-251023` q4.1-q5.4；`SOUL-OFAQ-260114` p21 | 3B / 3C 有战后 control update、contested、battlefield resolutions 与重连展示代表证据 | 战斗 / 法术对决期间冻结控制，关闭后释放，全控制权改变卡与替代效果 | 阶段 4 |
| conquer / hold scoring | `CORE-260330` rules 315.2.b.2、461-464；`SOUL-JFAQ-260114` p4-p5 | 3B 有 `BATTLEFIELD_HELD` / `BATTLEFIELD_CONQUERED` 代表路径和 snapshot 结果 | scoring order、全战场卡、得分替代、付费触发拒付、同时触发排序 | 阶段 4 |
| standby visibility | `CORE-260330` rules 107.2-107.3、315.2.b.2、319-323；`JFAQ-251023` q5.x | 3B 有 `standbyObjectIds`、`faceDownStandbyCount`、非法待命 cleanup 与隐藏信息代表路径 | 全 standby 卡族、失控待命所有时机、freeze 期间不提前移除、正式 DTO | 阶段 4 |
| cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-q5.2；`SOUL-OFAQ-260114` p19-p20 | 3B 已有 central queue 最小 task view、active task、blocking guard 和代表 cleanup | 所有 command / stack / trigger / move / enter / leave / damage / power change 统一 enqueue，repeat-until-stable completion audit | 阶段 4 |

## 4. ORDER_TRIGGERS / 多触发排序

当前证据：

- 阶段 2 已关闭 command/schema skeleton：`ORDER_TRIGGERS(triggerIds)`、malformed payload `INVALID_PAYLOAD`、`ActionPromptContracts` 名称。
- B 已实现最小 runtime window：prompt metadata 包含 `orderingPlayerId`、`orderedTriggerIds`、`triggerIds`、`triggers`、`triggerChoices`、`legalOrderingConstraints`、`triggeredByEventKind`。
- command 支持 `orderedTriggerIds`，并兼容既有 `triggerIds`。
- 合法排序会清空 `TriggerQueue`、按提交顺序加入 `StackItems`、设置 priority player，并广播 `TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK`。
- 当前有 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 事件、部分 `triggerQueue` / trigger view，以及 E stage3D 矩阵 overlay / `ORDER_TRIGGERS` 证据文档。
- C 已实现 `ORDER_TRIGGERS` UI：上移 / 下移排序、提交 `orderedTriggerIds`，不在前端结算触发。
- 验证记录：B 侧 `ConformanceFixtureShapeTests` 109/109 通过、full `dotnet test Riftbound.slnx --no-restore` 3333/3333 通过、`git diff --check` 通过；C 侧 build / smoke / `stage3-preflight.mjs` 通过。
- 规则依据入口：`CORE-260330` rules 333-340、383.3.d-383.3.e；`JFAQ-251023` q2.2-q2.3、q2.5；战斗初始结算链还关联 `CORE-260330` rules 454-461 与 `JFAQ-251023` q2.3-q2.4。

仍缺 P0：

- 完整 trigger engine 未完成。
- 完整 effect resolution 未完成。
- APNAP / 跨控制者复杂排序未完成。
- battle initial stack 全规则未完成，尤其进攻 / 防守触发特殊顺序、防守方触发后置、同时触发的多玩家窗口。
- trigger cost / decline / payment 未接入完整 PaymentEngine。

阶段 4 建议：

- 以已完成的最小 `ORDER_TRIGGERS` runtime 为基线，继续扩 APNAP / 跨控制者复杂排序。
- 扩 battle initial stack / attacker-defender trigger order。
- 接触发费用、decline 与 PaymentEngine，避免把支付、排序和战斗初始栈一次性混在同一改动里。

## 5. 阶段 4 / 最终验收边界

必须进入阶段 4：

- `ORDER_TRIGGERS` 完整 trigger engine、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment。
- priority / focus 与 `SPELL_DUEL_ACTION` 正式 payload，全反应 / 迅捷 / 反制链。
- 完整 battle task lifecycle：start、response、damage assignment、cleanup、result、close、next task。
- `ASSIGN_COMBAT_DAMAGE` 全规则矩阵：壁垒、后排、同优先级、负战力、不可分配、替代/预防。
- battle cleanup / battlefield control freeze-release / standby cleanup / conquer-hold scoring order。
- cleanup queue 全触发面和 repeat-until-stable 审计。
- PaymentEngine 完整化：`DECLINE_PAY_COST`、替代 / 额外费用、触发费用、非出牌支付窗口。
- 正式 snapshot/prompt DTO 冻结和双窗口隐藏信息 smoke。

必须留到最终验收：

- 最终正式 18 步 E2E，以同一连续正式牌局覆盖双人窗口、隐藏信息、复杂 prompt、战斗 / 法术对决、断线重连、终局。
- 1009 张卡 full-official 覆盖矩阵、官方文本 / FAQ 证据、自动化执行与缺口清零。
- LayerEngine / 替代 / 预防 / 持续效果全模型。
- replay / recovery / determinism 全命令、全恢复、随机边界 property。
- 产品 UI polish、可访问性、正式错误文案和发布级验收。

## 6. 是否允许进入阶段 4

A 主控 final validation 已通过，第三阶段可判定 **DONE**，可以准备进入阶段 4。这个结论只表示阶段性最小切片完成；这不是 READY，不是最终验收版 18 步 E2E，不是 1009 全量阶段。

A 主控最终验证记录：

- `git diff --check`：通过。
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：3333/3333 通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`：通过。
- `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api`：通过。

阶段 4 开始前建议由 A 明确：

- 阶段 4 首个 P0 切片选完整 trigger engine / APNAP，还是 battle/control freeze-release。
- B/C/E/D 的写入锁和不得并行修改文件。
- 阶段 4 每个切片的测试证据格式：规则依据、实现状态、测试命令、失败零副作用、仍缺口。

## 7. NOT READY 理由

项目仍 **NOT READY**，理由如下：

- 第三阶段 preflight / smoke 不是最终验收版 18 步 E2E。
- 1009 张卡没有 full-official 覆盖完成，也不能宣称全量官方卡已实现。
- `ORDER_TRIGGERS` 已有最小 runtime，但完整 trigger engine、完整 effect resolution、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment 仍未完成。
- 完整 PaymentEngine、LayerEngine、替代/预防/持续效果模型仍未完成。
- 完整 battle / spell duel lifecycle、battle initial stack、full damage assignment matrix、battle cleanup 全路径仍未完成。
- cleanup queue 全触发面、control freeze/release、standby 全时机、conquer/hold scoring order 仍未清零。
- 正式 DTO、隐藏信息三层断言和产品级前端验收仍需阶段 4 / 最终验收补齐。

## 8. 3D checkpoint commit 建议文件列表占位

最终由 A 汇总。D 建议候选文件：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`

明确排除：

- `riftbound-dotnet.sln`
- 服务端 C#、前端源码、测试、卡牌覆盖矩阵文档
