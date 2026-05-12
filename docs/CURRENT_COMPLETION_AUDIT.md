# 符文战场当前 Completion Audit

审计日期：2026-05-13
审计结论：**NOT READY**

本文件是 active goal 的当前收口审计清单，不代表最终完成验收。只有当本文所有 P0/P1 阻断清零、后端 full test、前端 build、Browser smoke / E2E 与隐藏信息检查全部通过后，才允许把 goal 标记为 complete。

## 0. 2026-05-13 最新状态补充

当前最新 Stage 4C checkpoint：`5ef4a45 checkpoint: complete stage 4C long sword equipment evidence`。Stage 4C-76 `Long Sword` / `SFD·022/221` / `FU-5accdd09f9` 已完成代表性 equipment play / explicit-target rejection / minimal assemble identity evidence-only overlay 与验证；项目整体仍 **NOT READY**。

4C-76 不修改功能代码，只把既有服务端权威 Long Sword 证据入账：ordinary hand `PLAY_CARD` 支付 2 mana、0 目标入栈，stack / pass-pass 后源牌进入控制者基地成为 `CARD_TYPE:EQUIPMENT` / `武装` / `灵便` 装备对象；显式目标打出被拒绝且无状态变化；最小 `ASSEMBLE_RED` 贴附与 owner/controller 身份保持作为邻接代表证据记录。Focused / primary regression 命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LongSword|FullyQualifiedName~P4AssembleEquipmentCommandLongSwordAttachFixture|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P5EquipmentDetachesWhenHostDestroyedFixture"
```

结果为 passed，11 passed / 0 failed / 11 total。追加回归 `FullyQualifiedName~LongSword|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~Equipment|FullyQualifiedName~Attached|FullyQualifiedName~Attach|FullyQualifiedName~MoveUnit|FullyQualifiedName~NonUnitSource` 通过 336/336；backend full 通过 3754/3754；frontend build 通过；Chrome smoke 通过。4C-76 只声明 narrow Long Sword representative equipment evidence recorded，不作为 READY 或 full-official 证据；Agile reaction attach、完整装备生命周期、LayerEngine 装备修正、完整 PaymentEngine、完整 FEPR target matrix 仍 deferred。

当前授权边界：用户已明确“在当前 goal 完成前不需要再申请授权”。本轮 A 继续保持主控 / 验收职责；4C-76 由 A 基于 matrix 风险筛选做 evidence-only 覆盖入账、复核、验证和文档收口。后续在 current goal 内可继续按既定写锁、验证门槛和 checkpoint 规则推进。

## 0.1 Active Goal 门槛到证据映射

| Active goal 要求 | 当前证据 | 当前状态 |
|---|---|---|
| A 作为主控架构 / 规划 / 验收 agent，不默认亲自写功能代码 | `docs/A_MASTER_AGENT_GOAL.md` 与 `docs/CURRENT_A_MASTER_CHECKPOINT.md` 明确 A 边界；4C-56 修复已按用户授权复用 B / Maxwell，A 做复核、验证和文档收口 | 满足主控边界 |
| 服务端保持唯一规则权威 | `docs/CURRENT_SERVER_RULE_AUDIT.md` 与本文件第 3 / 6 节记录服务端 authoritative snapshot / prompt / command guard 模型 | 方向满足，但仍有 P0/P1 规则缺口 |
| 前端只展示并提交服务端 `ActionPrompt` / authoritative snapshot 支持的合法操作 | 本文件第 5 / 6 / 9 节记录前端候选驱动与多批 Chrome smoke；最终 18 步 E2E 仍缺 | 部分验证，未达到最终验收 |
| P0/P1 阻断清零 | 本文件第 4 / 11 节与 `docs/CURRENT_SERVER_RULE_AUDIT.md` 仍列出 P0-002 / P0-003 / P0-004 / P0-005、P1 LayerEngine / 关键词 / 全卡证据；4C-56 blocker 已修复但不清零全局 P0/P1 | 未完成 |
| 后端 full test 当前 HEAD 全绿 | 4C-76 入账后 focused Long Sword regression 11/11、equipment / attach / move adjacent regression 336/336、backend full 3754/3754 均通过 | 本轮满足，最终验收前仍需重跑 |
| Chrome smoke 通过 | 4C-76 入账后 frontend build 通过，Chrome smoke 通过 | 本轮满足，最终验收前仍需正式 E2E |
| 正式 18 步 E2E 通过 | 本文件第 9 节明确缺一条完整覆盖 `docs/任务补充.md` 18 步最低流程的双浏览器或等效 E2E | 未完成 |
| 卡牌覆盖矩阵完成 | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C76` 回填为 representative evidence recorded，但 1009/811 full-official coverage 仍未完成 | 未完成 |
| 最终 completion audit 输出 READY 后才允许标记 complete | 本文件审计结论仍为 **NOT READY**；未调用 `update_goal complete` | 未完成 |

## 1. 修改文件列表

2026-05-13 Stage 4C-76 representative evidence 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

历史第二百五十九批修改：

- 第三百四十五批追加修改：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
  - `docs/CURRENT_COMPLETION_AUDIT.md`
  - `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
  - `docs/CURRENT_SERVER_RULE_AUDIT.md`

- `src/Riftbound.Api/Program.cs`
- `src/Riftbound.DevUi/package.json`
- `src/Riftbound.DevUi/scripts/check-user-facing-text.mjs`
- `src/Riftbound.DevUi/src/services/matchSocket.ts`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

本轮 active goal 的累计源码、测试和文档变更以 `git log`、`docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 与 `docs/CURRENT_SERVER_RULE_AUDIT.md` 的批次记录为准。

## 2. 新增文件列表

2026-05-13 Stage 4C-76 representative evidence 新增文档：

- `docs/CURRENT_STAGE4C_BATCH76_LONG_SWORD_EQUIPMENT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH76_LONG_SWORD_EQUIPMENT_EVIDENCE.md`

2026-05-13 Stage 4C-76 representative evidence 新增测试：无；本批复用既有 conformance tests，并只做矩阵与文档入账。

历史第二百五十九批新增：

- `src/Riftbound.DevUi/scripts/check-user-facing-text.mjs`

第二百一十四批已新增：

- `src/Riftbound.DevUi/src/utils/errors.ts`

第二百一十三批已新增：

- `src/Riftbound.DevUi/src/utils/redaction.ts`

第二百零九批已新增：

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
- 第三百四十五批已补一段 seeded battle/control/reconnect smoke，并修复正式流程中暴露的基地单位具体战场移动与条件减费 prompt 合法性缺口；完整正式 18 步 E2E 仍需最终 completion audit 单独通过。
- 继续补响应窗口、复杂费用、目标法术、战场得分与胜负结算的产品级真实操作 smoke。
- 确认所有玩家可见页面不再裸显协议词、对象 ID、raw enum、fixture/debug 文本或未授权隐藏信息。

## 6. 接口契约说明

- Web 入口通过 API/SignalR 获取房间状态、snapshot、prompt、events，并提交 `SUBMIT_DECK`、`READY`、`MULLIGAN`、`PLAY_CARD`、`MOVE_UNIT`、`DECLARE_BATTLE`、`PASS_PRIORITY`、`PASS_FOCUS`、`TAP_RUNE`、`RECYCLE_RUNE`、`ASSEMBLE_EQUIPMENT`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`END_TURN`、`SURRENDER` 等服务端已公开候选动作。
- 前端只能提交当前 prompt 中 enabled candidate 与 source/target/payment requirement 支持的参数；缺 sourceRequirement、缺 cardNo、缺合法目标或 disabled candidate 时，产品 UI 不应提供可点击提交入口。
- 服务端应拒绝手写越权命令，并保持失败命令不改变 authoritative state。

## 7. 隐藏信息保护检查结果

当前已有多批测试和 Chrome smoke 证明：对手手牌、隐藏待命、未知对象、内部对象 ID、stack/cleanup/task id 不应进入非授权玩家可见正文。最近的 object redaction smoke 覆盖了隐藏卡详情和页面正文；第二百一十二批确认官方开局起手调整候选只显示卡号；第二百一十三批确认任务队列 activeTaskId、cleanup/task id 与 raw reason 不进入玩家可见正文；第二百一十四批确认错误日志正文不再显示 raw error code 或英文内部错误消息；第二百一十五批确认卡牌详情操作 composer 的异常 fallback 不再显示 objectId、abilityId 或 raw cost id；第二百一十六批确认开发场景事件日志不再显示 development-only seed 名称或 scenario id；第二百一十七批确认 cleanup task 阻塞 prompt/error 只显示中文原因，不显示 raw task kind；第二百一十八批确认规则队列正文把 `RECALL_UNATTACHED_EQUIPMENT` 显示为“装备清理”，不显示 raw cleanup kind/reason 或未贴附装备对象 ID；第二百一十九批确认资源条把 typed rune trait 显示为“红色符能 2”，不显示 `red:2` 这类协议键；第二百二十批确认结算链 stack item destination 显示为“去向：战场”，不显示 `BATTLEFIELD:P1-MAIN` 或 raw `BATTLEFIELD`；第二百二十一批确认服务端 disabled candidate reason 显示“横置符文 / 回收符文”等中文行动名，不显示 `TAP_RUNE` / `RECYCLE_RUNE`；第二百二十二批确认前端共享 formatter 对未来未知协议 action/phase/window/status 降级为中文占位；第二百二十三批确认官方起手候选 tooltip 显示“起手调整候选”，不显示英文内部 reason 或手牌对象 ID；第二百二十四批确认事件/错误日志标题 hover 不再暴露 `event.kind` 或 `error.code`；第二百二十五批确认卡牌详情操作选择 tooltip 过滤内部英文/协议 reason；第二百二十六批确认行动面板、房间页和卡牌详情中的 prompt/candidate reason 正文与 title 统一过滤，不显示内部英文 reason、协议 destination 或对象 ID；第二百二十七批确认规则队列 phase/result fallback 不再回显未知服务端枚举；第二百二十八批确认卡牌详情不可组合 warning 过滤 `unsupportedReason`，不显示 raw ability id、对象 id 或 sourceRequirements metadata；第二百三十三批确认非当前行动玩家手写 `DECLARE_BATTLE` 被 START_BATTLE 队列拒绝时，错误消息显示中文“开始战斗”且不含 raw `START_BATTLE`；第二百三十四批进一步确认当前行动玩家提交错误战场声明时，服务端错误消息为中文且不含 raw `DECLARE_BATTLE` / `START_BATTLE`；第二百三十五批确认非法窗口的让过优先权/让过焦点错误消息为中文且不含 raw `PASS_PRIORITY` / `PASS_FOCUS`；第二百三十六批确认符文资源动作未知牌号运行时错误消息为中文且不含 raw `TAP_RUNE` / `RECYCLE_RUNE`；第二百三十七批确认结束回合非法窗口/玩家错误消息为中文且不含 raw `END_TURN`；第二百三十八批确认正式起手调整多条拒绝错误消息为中文且不含 raw `MULLIGAN`；第二百三十九批确认待命埋伏多条拒绝错误消息为中文且不含 raw `HIDE_CARD`；第二百四十批确认待命翻开/反应多条拒绝错误消息为中文且不含 raw `REVEAL_CARD`；第二百四十一批确认移动单位多条拒绝错误消息为中文且不含 raw `MOVE_UNIT`；第二百四十二批确认无对手投降拒绝错误消息为中文且不含 raw `SURRENDER`；第二百四十三批确认出牌来源多条拒绝错误消息为中文且不含 raw `PLAY_CARD`；第二百四十四批确认已结束对局拒绝后续命令错误消息为中文且不含 raw action kind；第二百四十五批确认启动技能来源控制权拒绝错误消息为中文且不含 raw `ACTIVATE_ABILITY`；第二百四十六批确认传奇行动来源多条拒绝错误消息为中文且不含 raw `LEGEND_ACT`；第二百四十七批确认装备装配未开放路径错误消息为中文且不含 raw `ASSEMBLE_EQUIPMENT`；第二百四十八批确认伏击出牌未开放路径错误消息为中文且不含 raw `PLAY_CARD mode AMBUSH`；第二百四十九批确认回合开始非回合玩家推进拒绝错误消息为中文且不含 raw `TURN_START`；第二百五十批确认启动技能未开放路径错误消息为中文且不含 raw `ACTIVATE_ABILITY`；第二百五十一批确认战场静态禁止出牌错误消息为中文且不含 raw `PLAY_CARD`；第二百五十二批确认传奇行动未开放与非法时点错误消息为中文且不含 raw `LEGEND_ACT`；第二百五十三批确认声明战斗未开放路径错误消息为中文且不含 raw `DECLARE_BATTLE`；第二百五十四批确认启动技能时点、目标、来源、身份、横置和资源错误消息为中文且不含 raw `ACTIVATE_ABILITY` 或英文技能说明；第二百五十五批确认战场效果目标选择错误消息为中文且不含英文战场效果说明或 raw `DECLARE_BATTLE`；第二百五十六批确认传奇触发目标选择错误消息为中文且不含英文 legend trigger 说明或 raw `PLAY_CARD`；第二百五十七批确认正式卡组、房间准备、未开局/已结束提交、未知命令和重复 intent 错误消息为中文且不含英文卡组/房间诊断或 raw unsupported command；第二百五十八批确认房间、重连、开发测试状态和空 id 错误消息为中文且不含英文房间/重连/seed 诊断；第二百五十九批确认前端连接超时 fallback、API 行为规格 404 与已收口房间错误文案被 `check:user-facing-text` 构建门禁覆盖。

最终验收前仍需再跑一次长链路隐藏信息检查，覆盖正式 deck、起手、手牌、牌堆顺序、面朝下待命、reconnect/replay 视角。

## 8. 测试命令与测试结果

最近批次证据：

- 后端 build 与 `GameHubJoinTests` 在最近前端/文档收口批次通过。
- 前端 `npm run build` 在最近前端收口批次通过。
- 后端 full test 最近完整通过记录见 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 批次记录；最终验收前必须重新运行当前 HEAD 的 full test。

历史第二百五十九批是前端/API fallback 文案与构建门禁收口批。`MatchSocket` 在 Join/Reconnect 等待超时时返回中文错误，行为规格 API 单卡未命中返回中文 404 message；`npm run build` 现在会执行 `check:user-facing-text`，防止关键玩家可见英文 fallback 回流。该历史批次曾记录后端 full test 3157/3157 通过、前端 build 通过。此前第二百三十二批 Chrome 插件房间 `smoke-battlefield-held-score-1778247059745` 已覆盖战场得分 UI 代表路径。

当前 2026-05-12 Stage 4C-59 representative guard 修复后，focused ZenithBlade guard 过滤测试通过 15/15，ZenithBlade / Stun / ActionPrompt / Prompt 回归过滤测试通过 154/154，backend full 通过 3701/3701，frontend build 通过，Chrome smoke 通过。本轮只证明 4C-59 narrow representative guard 与基本 UI smoke，不替代最终 18 步 E2E。

## 9. Browser smoke / E2E 结果

已有大量 Browser/Chrome smoke 覆盖中文化、候选展示、隐藏信息、spell duel cleanup、battle result、reconnect 等代表路径。Codex Chrome Extension 当前已确认可通信，第二百一十六批 Chrome smoke 使用房间 `smoke-scenario-redaction-1778238780476` 覆盖 `basic-play` seed 事件日志；页面显示“载入测试状态 / 测试状态已载入”，正文不含 `basic-play`、`DEV_SCENARIO_SEEDED`、`开发测试场景已载入`、`SeedScenario` 或 `scenarioId`，应用自身 runtime error 0，并在结束后清理测试标签和临时服务进程。第二百一十八批 Chrome smoke 使用房间 `smoke-unattached-equipment-1778239965401`，P2 页面连接并由后台 P1/P2 seed `battlefield-unattached-equipment-cleanup`；规则队列显示“阶段：状态清理 / 活动任务：处理中 / 装备清理：装备脱离清理”，prompt 显示“等待服务端处理任务队列：装备清理”，正文不含 raw cleanup kind/reason 或对象 ID。第二百一十九批 Chrome smoke 使用房间 `smoke-rune-trait-label-1778240000001`，P1 页面连接并由后台 P2 seed `typed-power-payment`；资源条显示“法力 1 / 符能 2 / 红色符能 2”，正文不含 `red:2`、`red : 2` 或 `red 2`。第二百二十批 Chrome smoke 使用房间 `smoke-zone-label-field-1778240000004`，P1 打出单位到 `BATTLEFIELD:P1-MAIN` 后规则队列显示“去向：战场”，正文不含 `BATTLEFIELD:P1-MAIN` 或 raw `BATTLEFIELD`。第二百二十三批 Chrome smoke 使用房间 `smoke-mulligan-title-1778242041908`，正式提交卡组/准备后在起手调整页面显示 4 个候选，title 全为“起手调整候选”，正文和 title 不含 `opening hand mulligan candidate` 或 `P1/P2-*` 手牌对象 ID，应用 error 0。第二百二十四批 Chrome smoke 使用房间 `smoke-log-title-1778242330553`，事件日志显示“载入测试状态”，`.event-log strong[title]` / `.room-log-list strong[title]` 数量为 0，正文不含 `DEV_SCENARIO_SEEDED`、`MATCH_STARTED`、`ROOM_FULL` 或 `UNSUPPORTED_COMMAND`，应用 error 0。第二百二十五批 Chrome smoke 使用房间 `smoke-choice-title-1778242626275`，打开卡牌详情后页面和 title 不含 `implemented payable PLAY_CARD source`、`implemented coarse battlefield destination`、`required for precise battlefield movement`、`P1-HAND` 或 `P1-MAIN`，应用 error 0。第二百二十六批 Chrome smoke 使用房间 `smoke-reason-filter-1778243194494`，P1 通过设置页写入 `serverUrl=http://127.0.0.1:5093`、`playerId=P1` 后连接对战页，后台 P2 seed `basic-play`，打开《魔法小仙灵》详情后显示“当前玩家普通开环行动 / 服务端可提交操作”，正文和 title 不含 `implemented payable PLAY_CARD source`、`implemented coarse battlefield destination`、`required for precise battlefield movement`、`opening hand mulligan candidate`、`P1-UNIT-MIGHTY-FAERIE`、`P1-MAIN-001` 或 `BATTLEFIELD:P1-MAIN`，应用 error 0。第二百二十七批在 Chrome 插件本地导航被 `ERR_BLOCKED_BY_CLIENT` 阻断后，改用后台 Playwright + 系统 Chrome headless，房间 `smoke-stack-label-headless-1778243871233` 显示“阶段：空闲”，正文不含 `IDLE`、`BATTLE_TASKS`、`BATTLEFIELD_TASKS`、`SPELL_DUEL_TASKS`、`STATE_BASED_CLEANUP`、`CONTROL_RESOLVED`、`NO_RESULT` 或 `CLOSED`，应用 error 0。第二百二十八批 Chrome 插件 smoke 使用房间 `smoke-unsupported-check`，P1/P2 均通过真实页面入座，后台 seed `battlefield-legend-attach-armament`；P1 打开《圣锤之毅》详情后显示中文不可组合 warning 且 `确认传奇行动` disabled，正文/title 不含 raw ability id、对象 id、`unsupportedReason`、`composable`、`targetChoicesByIndex` 或 `sourceRequirements`，应用 error 0。第二百二十一批为服务端 prompt reason 文案契约修正，第二百二十二批为前端 formatter 未知协议 fallback 修正，均未启动新的 API/Vite/Chrome smoke；5092/5093/5094/5175/5176/9223/9224 保持无监听。最近几次 Chrome smoke 都只记录扩展脚本 autoplay `NotAllowedError` 或 favicon 404 这类非应用噪声，过滤后应用 runtime error 0，结束后已清理测试标签和临时服务进程。

第二百三十批新增一条正式房间主流程探针证据：Chrome 插件房间 `room-vnpnxy` 完成创建/加入房间、合法卡组、准备、起手、首回合、召符文、抽牌、横置符文、出单位、结算链双方让过、单位移动到战场、结束回合、P2 回合开始和 reload/reconnect 恢复；同一房间由后台 SignalR 提交 P2 投降后，headless 结果页显示“胜者：P1”。本批结束后已 finalize Chrome 标签并清理 API/Vite/headless 进程，`5092/5093/5094/5175/5176/9223/9224` 无监听。

第二百三十一批补齐纯前端投降确认 smoke：Chrome 插件房间 `smoke-surrender-confirm-1778246799998` 中，P1 页面连接，后台 P2 入座并 seed `basic-play`；P1 点击“投降”后显示“确认投降 / 取消”，取消可收起，再次“投降 -> 确认投降”后结果页显示“胜者：P2”。smoke 后已 finalize Chrome 标签并清理 API/Vite 进程，目标端口无监听。

第二百三十二批补充战场得分真实 UI smoke：Chrome 插件房间 `smoke-battlefield-held-score-1778247059745` 中，P1 从服务端行动提示打开《大力仙灵》详情，选择服务端候选战场 `SFD·214/221` 与防守单位 `SFD·125/221`，点击“确认声明战斗”；页面显示“声明战斗 / 造成伤害 / 单位摧毁 / 据守战场 / 战场触发结算 / 支付费用 / 获得分数 / 战斗结束 / 战场控制结算”，P2 分数为 `1/8`。应用 runtime error 0，结束后已清理 Chrome 标签和 API/Vite 进程。

第二百五十九批仅改前端/API fallback 文案和构建门禁，无业务交互流程变更；本批未启动新的 API/Vite/Chrome smoke，提交前目标端口保持无监听。

最终仍缺一条完全覆盖 `docs/任务补充.md` 18 步最低流程的双浏览器或等效 E2E：同一连续正式牌局还没有覆盖战场争夺/战斗与战场得分整合；当前战场得分证据仍是 development seed 代表路径。

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
