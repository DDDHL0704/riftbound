# 符文战场当前 Completion Audit

审计日期：2026-05-08
审计结论：**NOT READY**

本文件是 active goal 的当前收口审计清单，不代表最终完成验收。只有当本文所有 P0/P1 阻断清零、后端 full test、前端 build、Browser smoke / E2E 与隐藏信息检查全部通过后，才允许把 goal 标记为 complete。

## 1. 修改文件列表

当前第二百一十批修改：

- `src/Riftbound.DevUi/src/pages/LobbyPage.tsx`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

本轮 active goal 的累计源码、测试和文档变更以 `git log`、`docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 与 `docs/CURRENT_SERVER_RULE_AUDIT.md` 的批次记录为准。

## 2. 新增文件列表

当前批次无新增文件。第二百零九批已新增：

- `docs/CURRENT_COMPLETION_AUDIT.md`

未跟踪 `riftbound-dotnet.sln` 是本地预期文件，不属于本轮审计交付，也不得提交。

## 3. 关键架构说明

- 服务端仍是唯一规则权威：Web 前端只读取 authoritative snapshot、服务端行动提示、事件和候选元数据，不在浏览器内裁决费用、时点、目标、战场控制或胜负。
- SignalR 房间层按玩家发送 snapshot/prompt，隐藏信息由服务端视角裁剪；前端只负责产品化展示与提交服务端候选中允许的命令。
- `MatchSession` 串行处理命令并只在 accepted 结果后更新权威状态；前端 reconnect/reload 必须从服务端快照恢复。
- Development-only seed 只用于 smoke 和规则回归证据，不能替代正式官方构筑、起手和完整 1v1 主流程验收。

## 4. 服务端规则补齐项

已补到可验证代表路径的部分包括：官方 deck/opening/mulligan 入口、按玩家视角快照、对象位置索引、typed rune pool、部分 PaymentEngine 元数据、spell duel 焦点恢复、代表性 battle/declare battle 路径、部分 cleanup loop、战场任务视图、隐藏信息和大量来源/目标控制权 guard。

仍存在 P0/P1 阻断：

- P0-002：完整 battlefield standby/control/held/conquer task lifecycle 仍未官方化。
- P0-003：central cleanup task queue 仍未覆盖所有状态变化、替代效果和进出战场路径。
- P0-004：spell duel / battle 仍是大量代表路径，缺完整官方 pending/focus/initial-stack/伤害分配/清理状态机。
- P0-005：完整 PaymentEngine 与 reaction payment window 仍未统一，ACTIVATE_ABILITY、LEGEND_ACT、MOVE_UNIT、ASSEMBLE_EQUIPMENT 等仍有代表路径边界。
- P1：LayerEngine、完整关键词、全官方卡牌逐条证据、长期 replay/hash 审计仍未达到最终 READY。

## 5. 前端页面完成项

已完成并有批次证据的页面/模块包括：首页、图鉴、卡组、设置、房间页、对战桌面、行动面板、卡牌详情、规则队列、事件日志、结算页、中文化展示、内部对象标识脱敏、隐藏信息展示和服务端候选驱动交互。

仍需收口：

- 以双浏览器或等效 E2E 覆盖 `docs/任务补充.md` 的 18 步最低主流程。
- 继续补响应窗口、复杂费用、目标法术、战场得分与胜负结算的产品级真实操作 smoke。
- 确认所有玩家可见页面不再裸显协议词、对象 ID、raw enum、fixture/debug 文本或未授权隐藏信息。

## 6. 接口契约说明

- Web 入口通过 API/SignalR 获取房间状态、snapshot、prompt、events，并提交 `SUBMIT_DECK`、`READY`、`MULLIGAN`、`PLAY_CARD`、`MOVE_UNIT`、`DECLARE_BATTLE`、`PASS_PRIORITY`、`PASS_FOCUS`、`TAP_RUNE`、`RECYCLE_RUNE`、`ASSEMBLE_EQUIPMENT`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`END_TURN`、`SURRENDER` 等服务端已公开候选动作。
- 前端只能提交当前 prompt 中 enabled candidate 与 source/target/payment requirement 支持的参数；缺 sourceRequirement、缺 cardNo、缺合法目标或 disabled candidate 时，产品 UI 不应提供可点击提交入口。
- 服务端应拒绝手写越权命令，并保持失败命令不改变 authoritative state。

## 7. 隐藏信息保护检查结果

当前已有多批测试和 Chrome smoke 证明：对手手牌、隐藏待命、未知对象、内部对象 ID、stack/cleanup/task id 不应进入非授权玩家可见正文。最近的 object redaction smoke 覆盖了隐藏卡详情和页面正文。

最终验收前仍需再跑一次长链路隐藏信息检查，覆盖正式 deck、起手、手牌、牌堆顺序、面朝下待命、reconnect/replay 视角。

## 8. 测试命令与测试结果

最近批次证据：

- 后端 build 与 `GameHubJoinTests` 在最近前端/文档收口批次通过。
- 前端 `npm run build` 在最近前端收口批次通过。
- 后端 full test 最近完整通过记录见 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 批次记录；最终验收前必须重新运行当前 HEAD 的 full test。

当前第二百一十批是前端文案收口批，`source ../../scripts/dev-env.sh && npm run build` 与 `git diff --check` 已通过。

## 9. Browser smoke / E2E 结果

已有大量 Browser/Chrome smoke 覆盖中文化、候选展示、隐藏信息、spell duel cleanup、battle result、reconnect 等代表路径。Codex Chrome Extension 当前已确认可通信，第二百一十批 Chrome smoke 已覆盖大厅页协议词收口并在结束后清理测试标签和临时服务进程。

最终仍缺一条覆盖 `docs/任务补充.md` 18 步最低流程的双浏览器或等效 E2E：创建/加入房间、合法卡组、准备、起手、首回合、召符文、抽牌、出单位、移动到战场、争夺/战斗、法术对决、双方让过、结束回合、重连恢复、战场得分和投降/胜利结算。

## 10. 仍未完成的 P2 项

P2 项暂不作为 complete 阻断，但应在 P0/P1 清零后继续排期：

- 更完整的新手引导、可访问性细节、动画/音效 polish。
- 更丰富的回放检索、调试筛选和开发者诊断视图。
- 非 P8 范围的账号、匹配、部署、监控和风控仍不进入当前 goal。

## 11. 是否仍存在 P0/P1 阻断

**是。** 当前仍存在 P0/P1 阻断，不能把 active goal 标记 complete。

## 12. 最终结论

**NOT READY**

下一步优先级：继续按 `docs/CURRENT_SERVER_RULE_AUDIT.md` 收口 P0-002/P0-003/P0-004/P0-005 和 P1 LayerEngine/全卡证据，同时补一条完整双浏览器或等效 E2E 主流程。
