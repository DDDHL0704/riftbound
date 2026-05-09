# 阶段 3 对战桌面与核心 1v1 流程审计

更新日期：2026-05-09
当前 HEAD：`4b41e81`
阶段 2 checkpoint：`dfc4bd4`
结论：**NOT READY**

本文是 D 文档 / 规则证据 / P0-P1 审计 agent 为阶段 3 建立的持续审计入口。阶段 3 的目标不是直接宣称完整官方规则完成，而是把本地双人 1v1 对战桌面的核心 happy path 与服务端权威规则边界持续对齐，直到 A 能用后端 full test、Chrome smoke、正式 18 步 E2E 和规则证据完成验收。

边界：

- D 只维护文档、规则证据、P0/P1 审计，不修改前端、服务端、测试或 E 覆盖矩阵。
- 阶段 2 已关闭的是“复杂命令没有正式 schema / malformed payload 没有稳定错误码”的子项；真实 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` runtime 仍未关闭。
- 阶段 3 可以先用服务端已经支持的代表路径做 smoke，但不得把代表路径误称为完整官方规则状态机。

## 0. 当前执行子阶段：3A

用户已将当前执行范围收窄为 **阶段 3A**。本宽阶段 3 审计文档保留为总图和后续 backlog，不回滚；当前验收优先级以 `docs/CURRENT_STAGE3A_PLAN.md` 为准。

3A 只收口：

- Smoke 基线。
- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 强类型复杂命令解析。
- `PAY_COST` 最小 runtime 切片。
- 对战桌面外壳安全接线，前端不得裁决规则。

3A 暂不进入：

- 最终正式 18 步 E2E。
- 1009 张卡 full-official 覆盖。
- 完整 battle / damage assignment / order triggers runtime。
- 完整 battlefield / standby / control / held / conquer lifecycle。

当前 3A P0：

| P0 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- |
| 3A-P0-001 Chrome smoke 基线 | C 并行工作区已有 smoke 脚本痕迹，但 D/A 尚未记录稳定验收证据 | C / A / D | 固化运行命令、覆盖点、console/network 与隐藏信息断言 |
| 3A-P0-002 三类复杂命令强类型映射 | B 并行工作区已出现三类 JSON command -> typed command mapper；D 尚未运行 build/test 或记录 recovery/full-payload 证据 | B / D | 补 Hub/recovery/mapper 证据，确认 malformed payload 与合法未开放 runtime 的稳定拒绝语义 |
| 3A-P0-003 `PAY_COST` 最小 runtime | schema 和 closed-window 拒绝已有；pending payment runtime 未开放 | B / E / C / D | 接最小 payment prompt -> command -> commit/decline 或失败语义 |
| 3A-P0-004 前端外壳不裁决规则 | C 有并行外壳改动，D 尚未验收 smoke | C / D | 证明只展示 snapshot/prompt，只提交服务端候选，不泄漏隐藏信息 |

## 1. 阶段 3 核心流程审计矩阵

| 流程 | 规则依据入口 | 当前实现状态 | 阻断分类 | 归属 agent | 下一步建议 |
| --- | --- | --- | --- | --- | --- |
| 创建 / 加入房间 | 工程会话契约；`CORE-260330` rules 107-129 隐藏信息边界 | `GameHub` join / reconnect / snapshot 路径已有测试；阶段 2 checkpoint 记录尚无正式 smoke 脚本 | 阻断 smoke | C 主实现 smoke；B 维护 Hub；D 审计 | C 建最小 Chrome smoke：双浏览器上下文创建/加入/重连，D 记录证据位置 |
| 选择卡组 / 提交卡组 | `CORE-260330` rule 103 构筑与开局区；官方 2026-04-27 卡牌快照 | `SUBMIT_DECK`、官方 deck session error localization 和构筑负例矩阵已有代表路径；未证明同一连续阶段 3 smoke | 阻断 smoke | B 服务端校验；C UI；E 卡牌数据；D 审计 | smoke 必须验证合法卡组可提交、非法卡组错误可见且不泄漏隐藏信息 |
| 准备 / 开始游戏 | 工程房间状态；`CORE-260330` rule 103 开局区 | `READY` / room prompt 已有；阶段 3 需要从 room 页推进到 match 页的连续证据 | 阻断 smoke | C 主实现；B 维护 session；D 审计 | Chrome smoke 覆盖双方准备、开始、初始 snapshot/prompt |
| 起手调整 | `CORE-260330` 开局流程；`rules-evidence-index.md` 中 mulligan / opening 证据 | `MULLIGAN` prompt、hand selection guard、replacement draw guard 已有代表测试 | 阻断 smoke | B 规则；C UI；D 审计 | 双窗口各只看到己方手牌；提交后不得泄漏对手手牌 cardNo |
| 第一回合进入 | `CORE-260330` p28-p29 rule 315；rule 481.7 | `p2-preflight-turn-start-*` 已覆盖召出符文、抽牌、燃尽等代表路径 | 阻断 smoke | B 规则；C UI；D 审计 | smoke 要断言 turn / active player / phase / prompt 从服务端 snapshot 推进 |
| 召符文 / 符文池 | `CORE-260330` p20 rules 164-167；p28-p29 rule 315.3 | 召出符文、短符文牌堆、第二行动玩家额外符文已有代表 fixture；typed `RunePool` 已有 | 阻断 smoke | B 规则；C UI；D 审计 | 阶段 3 smoke 只验证服务端 snapshot 展示，不让前端自行生成资源 |
| 打出卡牌 | `CORE-260330` rules 349+、355-357、377、403-405；`JFAQ-251023` q1.1-q2.5 | 大量 `PLAY_CARD` representative path 与 `COST_PAID` 包络已有；真实 `PAY_COST` runtime/PaymentEngine 未关闭 | 阻断 smoke；PaymentEngine 可在阶段 3 内继续 | B 主实现支付窗口；C 只提交候选；D 审计 | smoke 先选一张服务端候选可打的简单单位或法术；P0 仍记录 PaymentEngine 未完成 |
| 移动单位 | `CORE-260330` rules 107-129 区域；rules 187-189 控制；rules 319-323 清理 | 基地单位移动到具体战场、source control guard、具体战场 objectId 大小写已有 | 阻断 smoke | B 规则；C UI；D 审计 | smoke 覆盖 `MOVE_UNIT` 到服务端公开的具体战场 destination，snapshot 精确显示位置 |
| 争夺 / 结算链 / 法术对决 | `CORE-260330` rules 307-313、333-348；`JFAQ-251023` q2.2-q3.3、q4.1-q5.4 | 代表性 `TRIGGER_QUEUED` / stack / spell duel focus / battlefield contest smoke 已有；完整生命周期仍未官方化 | 可在阶段 3 内继续；完整生命周期仍 P0 | B 主实现；C 展示服务端窗口；E fixture；D 审计 | 阶段 3 smoke 可选争夺、结算链或法术对决之一作为核心链路，但不得关闭 battle/spell duel 全 P0 |
| 结束回合 | `CORE-260330` p29-p31 rules 316-318；p31-p33 rules 319-324；`JFAQ-251023` q5.1-q5.2 | `END_TURN`、特殊清理、符文池清空、推进到下一回合已有代表 fixture | 阻断 smoke | B 规则；C UI；D 审计 | smoke 需证明 `END_TURN` 只提交服务端允许动作，下一玩家 snapshot 正确 |
| 投降 / 胜负结算 | 胜负检查：`CORE-260330` p31-p33 rule 323.1；得分：rules 461-464；投降为产品会话契约 | `SURRENDER` command 存在；胜负 / match result 有代表路径；阶段 3 连续 smoke 未证明 | 阻断 smoke | B session；C result UI；D 审计 | smoke 至少覆盖投降或一条服务端胜负结算，result 不由前端推断 |
| 断线重连 / 隐藏信息 | `CORE-260330` rules 107-129；工程 reconnect 契约 | viewer snapshot、spectator redaction、reconnect smoke 有代表证据；阶段 3 双浏览器 DOM/store/WS 仍需证明 | 阻断 smoke | B visibility；C smoke；D 审计 | 每个阶段 3 smoke 都必须带 hidden-info 断言，不只看 UI 文案 |

## 2. B 阶段 3 服务端阻断关闭记录位

以下表格只预留记录位。B 后续每关闭一个服务端阻断，D 再填入实现状态、测试命令、证据链接和剩余缺口；在 D 复核前不得从 P0/P1 清单移除。

| ID | 阻断项 | 规则依据 | B 实现状态 | 测试证据 | 仍缺口 | D 审计结论 |
| --- | --- | --- | --- | --- | --- | --- |
| S3-BLOCK-ROOM-DECK | 房间 / 卡组 / 准备 / 开局连续链路 | `CORE-260330` rule 103；rules 107-129 | 待 B/C 同步 | 待填 | 未有正式阶段 3 smoke | OPEN |
| S3-BLOCK-MULLIGAN | 双人起手调整与隐藏信息 | `CORE-260330` 开局流程；rules 107-129 | 待 B/C 同步 | 待填 | 双窗口 DOM/store/WS 隐藏断言未收口 | OPEN |
| S3-BLOCK-FIRST-TURN | 第一回合召符文、抽牌、主阶段进入 | `CORE-260330` rule 315；rules 164-167 | 待 B 同步 | 待填 | 连续 smoke 未收口 | OPEN |
| S3-BLOCK-PLAY-PAY | 打出卡牌与支付窗口 | `CORE-260330` rules 349+、355-357、377、403-405 | 代表 `COST_PAID` 有；`PAY_COST` runtime 待实现 | 待填 | PaymentEngine / decline / choice validation 未关闭 | OPEN |
| S3-BLOCK-MOVE-CONTEST | 移动到战场、争夺、控制权变化 | `CORE-260330` rules 187-189、319-323、344-348、461-464 | 代表具体战场移动与争夺 smoke 有 | 待填 | 完整 control freeze / held / conquer lifecycle 未关闭 | OPEN |
| S3-BLOCK-STACK-DUEL | 结算链 / 法术对决 / 让过 | `CORE-260330` rules 307-313、333-348；`JFAQ-251023` q2.2-q3.3 | 代表 pass / focus / stack tests 有 | 待填 | 完整 pending / initial-stack / trigger ordering 未关闭 | OPEN |
| S3-BLOCK-ENDTURN | 结束回合、特殊清理、下一回合 | `CORE-260330` rules 316-324；`JFAQ-251023` q5.1-q5.2 | 代表 end-turn fixtures 有 | 待填 | central cleanup queue 未完整官方化 | OPEN |
| S3-BLOCK-RESULT | 投降或胜负结算 | `CORE-260330` rule 323.1；rules 461-464；产品会话契约 | 待 B/C 同步 | 待填 | 连续 result UI / session close smoke 未收口 | OPEN |

## 3. 阶段 2 已替代但阶段 3 仍未关闭的口径

- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 已有 command/schema skeleton 和 `INVALID_PAYLOAD`，但 runtime prompt、状态机、合法选择与结算仍未完成。
- 复杂 prompt 降级展示已能安全承接未知窗口，但不能替代正式支付、伤害分配、触发排序、法术对决专用交互。
- 0/负战力、具体战场 objectId 大小写已被阶段 1/2 修复与验收替代；阶段 3 只保留防回归，不再当作当前 P0。
- replay/final hash 旧口径已被 representative verifier / recovery smoke 替代；阶段 3 仍需全命令、全恢复、全随机路径 determinism 审计，不能宣称 READY。
- 代表性 battlefield contest smoke 不能替代完整 battlefield / standby / control / held / conquer lifecycle。
- 大量 `PLAY_CARD` / `ASSEMBLE_EQUIPMENT` 代表路径不能替代 1009 张卡 full-official 覆盖矩阵。

## 4. 阶段 3 当前 P0/P1 分类

### 阻断 smoke 的 P0

| P0 | 规则依据入口 | 当前实现状态 | 归属 agent | 下一步建议 |
| --- | --- | --- | --- | --- |
| S3-P0-001 最小 Chrome smoke 脚本 / 运行证据缺失 | A 目标文档阶段 3 与 18 步 E2E；工程验收契约 | 阶段 2 checkpoint 明确未发现既有 smoke 脚本；目前不能证明浏览器端创建/加入/对战桌面连续链路 | C 主实现；A 验收；D 记录 | C 先补最小 smoke，覆盖 create/join/deck/ready/start/mulligan/first turn |
| S3-P0-002 双人 room -> match 连续链路未证明 | `CORE-260330` rule 103；rules 107-129 | 后端与 UI 有分散代表测试；缺同一连续正式流程证据 | B/C；D 审计 | smoke 必须用两个 player context，不复用单客户端状态 |
| S3-P0-003 起手与隐藏信息浏览器断言未收口 | `CORE-260330` rules 107-129 | viewer snapshot 代表路径已有；DOM/store/WS 双窗口断言未形成阶段 3 证据 | B visibility；C smoke；D 审计 | 对手手牌、牌堆顺序、hidden metadata 不得进入非授权客户端 |
| S3-P0-004 第一回合召符文 / 抽牌 / 主阶段进入未在浏览器连续证明 | `CORE-260330` rule 315；rules 164-167 | `p2-preflight-turn-start-*` 后端 fixture 已有；UI smoke 未闭环 | B/C；D 审计 | 断言 turn/active/phase/runePool/hand count 均来自 snapshot |
| S3-P0-005 打出卡牌、移动、争夺或结算链的浏览器核心链路未闭环 | `CORE-260330` rules 333-357；rules 187-189；rules 344-348 | 单点代表测试与若干后台 smoke 有；正式阶段 3 连续 smoke 未收口 | B/C；D 审计 | 选一条最短服务端候选路径：打出简单牌 -> 移动 -> 争夺或 stack/pass |
| S3-P0-006 结束回合、投降或胜负结算未在阶段 3 smoke 收口 | `CORE-260330` rules 316-324、461-464；产品会话契约 | end-turn / result 代表路径已有；连续浏览器 result 未收口 | B/C；D 审计 | smoke 至少覆盖进入下一回合，并覆盖投降或服务端 winner/result |

### 可在阶段 3 内继续推进的 P0

| P0 | 规则依据入口 | 当前实现状态 | 归属 agent | 下一步建议 |
| --- | --- | --- | --- | --- |
| S3-P0-007 `PAY_COST` runtime / PaymentEngine | `CORE-260330` rules 131、135.2.e、162-167、356-357、377、403-405、414、416；`JFAQ-251023` q2.5 | schema skeleton 已有；代表 `COST_PAID` 路径已有；真实 pending payment、decline、Quote/Authorize/Commit 未完成 | B 主实现；C 等 schema；E fixture；D 审计 | 先接一条 `PAY_COST` pending window，覆盖 malformed / decline / insufficient / commit |
| S3-P0-008 `ASSIGN_COMBAT_DAMAGE` runtime / battle damage phase | `CORE-260330` rules 142-143、417、460；`JFAQ-251023` q6.1-q6.4 | schema skeleton 已有；代表 damage order 有；真实 assignment prompt/state machine 未完成 | B 主实现；E fixture；C 等 UI；D 审计 | 多单位、壁垒、后排、同优先级选择先做最小官方 fixture |
| S3-P0-009 `ORDER_TRIGGERS` runtime / trigger batch ordering | `CORE-260330` rules 333-340、383.3.d-383.3.e；`JFAQ-251023` q2.2-q2.3、q2.5 | schema skeleton 已有；部分 triggerQueue view 有；真实 ordering prompt 未完成 | B 主实现；E fixture；C 等 UI；D 审计 | 先覆盖同一控制者同时触发，再扩跨玩家回合顺序 |
| S3-P0-010 battlefield / standby / control / held / conquer lifecycle | `CORE-260330` rules 107.2-107.3、187-189、319-323、344-348、461-464；`JFAQ-251023` q4.1-q5.4 | 具体战场移动、代表争夺、部分控制结算已有；完整 board task model 未完成 | B 主实现；E fixture；C 展示；D 审计 | 把 control freeze、standby removal、held/conquer scoring 拆成可推进 task |
| S3-P0-011 central cleanup queue | `CORE-260330` rules 318-324；`JFAQ-251023` q5.1-q5.2 | `RunStateBasedCleanupLoop` 与部分 pending tasks 有；未覆盖所有状态变化 | B 主实现；E fixture；D 审计 | 统一 command/stack/trigger/move/damage/power change enqueue |
| S3-P0-012 spell duel / battle lifecycle | `CORE-260330` rules 307-313、333-348、454-461；`JFAQ-251023` q2.3-q3.3 | `SpellDuelState`、`BattleState`、关联 id 和焦点恢复有；`DECLARE_BATTLE` 仍代表同步路径 | B 主实现；E fixture；C 展示；D 审计 | 拆出 pending battle/spell duel task、initial stack、pass/focus/close/result |

### 暂不阻断阶段 3 初始 smoke、但阻断最终 READY 的 P1/P0

| 项 | 规则依据入口 | 当前实现状态 | 归属 agent | 下一步建议 |
| --- | --- | --- | --- | --- |
| 1009 张卡 full-official 覆盖矩阵 | `data/official/card-catalog.zh-CN.json`；所有 FAQ 卡牌条目 | E 已建矩阵 skeleton 与风险 Top20；未进入全量实现 | E 主；B 实现；D 审计 | 阶段 3 smoke 可用代表卡组，但最终 READY 必须回到全量矩阵 |
| LayerEngine / 持续 / 替代 / 禁止效果 | `CORE-260330` 连续效果、替代/预防相关规则；FAQ 高风险卡 | 多个代表路径有；完整 layer/timestamp/dependency 未完成 | B/E；D 审计 | 不阻断最小桌面 smoke，阻断正式规则助手与 READY |
| replay / recovery / determinism 全路径 | 工程恢复契约；隐藏信息 rules 107-129 | representative final hash / recovery smoke 有；全命令 property 未完成 | B；D 审计 | 阶段 3 smoke 要带 reload/reconnect，最终 READY 需全路径审计 |
| 产品级视觉 polish | 前端需求文档 | C 后续推进；不是 D 规则 P0 | C | 不得影响服务端权威与隐藏信息红线 |

## 5. D 阶段 3 后续审计动作

- 每次 B/C 报告阶段 3 阻断关闭，D 先核对 diff / 测试 / 规则依据，再更新本文与 `CURRENT_SERVER_RULE_AUDIT.md`。
- 每次 E 新增官方证据或 fixture，D 只同步索引入口，不改 E 覆盖矩阵。
- 任何“已通过 smoke”的记录必须带：命令、测试范围、房间/fixture 名、隐藏信息断言、仍缺口。
- 在 A 未完成正式 18 步 E2E 与 completion audit 前，所有阶段 3 文档继续保持 **NOT READY**。
