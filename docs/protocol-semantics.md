# 协议语义边界

更新时间：2026-04-29

## 1. 目的

本文件记录客户端提交意图和服务端权威结算之间的命令语义。它优先服务 P1 联机底座，避免继续继承旧 Java 中 `PASS -> TURN_ENDED` 的混淆。

规则依据见 `docs/rules-evidence-index.md`。当前三条 Java fixture 仍是 `legacyOracle`，不是最终规则裁决。

## 2. 协议 envelope

`WsClientMessage` 和 `WsServerMessage` 当前默认携带：

| 字段 | 当前值 | 说明 |
|---|---:|---|
| `protocolVersion` | 1 | 客户端/服务端协议兼容版本；破坏性协议变化时递增。 |
| `schemaVersion` | 1 | 当前 JSON envelope schema 版本；后续 upcaster 和 TypeScript DTO 生成以它为锚点。 |

P1 暂不实现 upcaster，也不为每个 payload 单独建立版本字段；后续前端 DTO 接入后再扩展。

## 3. 房间生命周期

| cmdType / Hub 方法 | 语义 | 当前实现状态 |
|---|---|---|
| `JoinRoom` | 玩家入座，前两名玩家获得稳定 `P1` / `P2` seat | 已实现；第三名玩家返回 `ROOM_FULL`。 |
| `READY` / `GameHub.Ready` | 玩家确认准备；双方都 Ready 后房间进入 `IN_PROGRESS` | 已实现；第一名 Ready 广播 `READY` + `PLAYER_READY`，第二名 Ready 广播 `START` + `MATCH_STARTED`。 |
| `Reconnect` | 通过 reconnect token 恢复玩家连接 | 已实现；服务端只持久化 `sha256:` token hash，重连成功轮换 token/hash。 |

状态流：`EMPTY -> SEATING -> IN_PROGRESS`，`FINISHED` 作为后续胜负结算状态保留。未进入 `IN_PROGRESS` 前提交规则命令会返回 `MATCH_NOT_STARTED`。

## 4. 让过与结束回合

| cmdType | 语义 | 规则语境 | 当前实现状态 |
|---|---|---|---|
| `PASS_PRIORITY` | 让过优先行动权 | FEPR/结算链闭环，依据 `CORE-260330` p34-p35 rules 335-340 | 普通主阶段没有优先行动权窗口时返回 `PHASE_NOT_ALLOWED`，不结束回合；有结算链项目时当前优先权玩家可让过，双方都让过后结算最新项目并回到普通主阶段。 |
| `PASS_FOCUS` | 让过焦点 | 法术对决开环，依据 `CORE-260330` p36 rules 347-348 | 普通主阶段没有法术对决焦点窗口时返回 `PHASE_NOT_ALLOWED`；法术对决中当前焦点玩家可让过，双方都让过后关闭法术对决并回到普通主阶段。 |
| `PLAY_CARD` | 从手牌打出卡牌 | 打出卡牌流程、目标选择、费用和结算链，依据 `CORE-260330` p39-p42 rules 355-356, p33-p35 rules 327-340 | 当前最小实现按 card behavior registry 支持 P2 preflight 法术：校验手牌、费用、场上/手牌/对手手牌/主牌堆顶部/废牌堆目标范围（含敌方单位、敌方战场单位、友方战场单位后敌方战场单位目标、单位后己方主牌堆顶部目标）、对象属性标签、模式、可选费用和强制额外费用，加入结算链后由服务端权威结算伤害、按来源单位战力造成伤害、按战斗状态筛选敌方单位的范围伤害、敌方战场单位范围伤害、敌方战场主目标加其他敌方战场单位溅射伤害、1-3 多目标单位伤害、本回合内受到的后续伤害翻倍、下一次伤害抵挡、本回合内下次受到伤害触发摧毁、当前场上单位受伤即摧毁、抽牌、目标控制者选择让来源控制者抽牌以避免伤害、主牌堆顶部二选一/三选一选择抽取并回收未选牌、主牌堆顶部三张中单位牌选择抽取并回收未选牌、主牌堆顶部五张不选择单位牌时回收全部已查看牌、洞察顶部牌回收、对手手牌非单位牌回收、眩晕、摧毁、双方各自选择摧毁己方单位、其他玩家选择非控制者单位后摧毁、总战力不高于 4 的多目标摧毁、回手、每名玩家可选单位回手、单位放到拥有者主牌堆顶/底、弃置/批量弃置、移动到基地、友方战场单位代表移动到基地后变为活跃状态、敌方战场单位代表移动到基地（含《魅惑妖术》与未启用等级 6 的《升龙踢》）、敌方战场单位移动到基地且不抽牌、按当前己方战场单位数动态选择任意数量友方战场单位移动到基地、打出单枚/多枚/回响重复单位指示物到基地（含 2 战力黄沙士兵和 3 战力精灵）、可选装备摧毁被跳过后的抽牌、无已控制战场时的基础抽牌、《完美谢幕》未支付回响时的四个模式分支、变为活跃/休眠状态、单体/群体/标签筛选/同战场友方正敌方负/同战场敌方范围负/标签种类动态/目标间动态/目标间互换/按目标序号作用的战力修正、属性减费、回收、回响、《惩戒》放逐替代和《高原血统》/《战术撤退》休眠召回基地替代等效果；`CARD_TYPE:UNIT` 对象标签暂用于《龙虎双雄》的顶部单位牌目标校验和《暗中破坏》的对手手牌单位牌排除。 |
| `END_TURN` | 表明主阶段没有要执行的自决行动，进入回合结束流程 | 普通开环主阶段，依据 `CORE-260330` p30 rules 316.1-316.6 和 p30-p31 rules 317.1-317.3 | `CoreRuleEngine` 已产出正式 P2 事件序列并自动串到下一回合开始；伤害移除、本回合内效果失效和清理重复事件已接入。 |
| `PASS` | 旧 Java 兼容命令 | 不作为最终规则语义 | 仅保留给 legacy fixture 和旧客户端兼容；新 fixture 不应再新增裸 `PASS`。 |

## 5. 实现约束

- 客户端只提交意图，不直接修改状态。
- `clientIntentId` 是 Ready/Submit 等命令的必填幂等键；空值返回 `CLIENT_INTENT_ID_REQUIRED`，服务端不会为命令随机生成 intentId。
- `ActionPrompt` 后续应按当前回合状态暴露 `PASS_PRIORITY`、`PASS_FOCUS` 或 `END_TURN`，避免同时暴露语义冲突的动作。
- `PLAY_CARD.optionalCosts` 当前承载已落地的额外费用选择：`"ECHO"`、`"EXHAUST_FRIENDLY_UNIT:<objectId>"`、`"DESTROY_FRIENDLY_POWERFUL_UNIT:<objectId>"` 和 `"DISCARD_HAND_CARD:<objectId>"`。其中摧毁友方强力单位虽然字段名沿用 `optionalCosts`，但对《牺牲》是强制额外费用，缺失或目标非法会由服务端拒绝。
- `PASS` 在 P1 期间只能用于 legacy fixture；若新测试需要让过，必须使用更具体的 command。
- Hub 错误统一返回 `ErrorDto(code, message)`；当前已覆盖 `ROOM_FULL`、`PLAYER_NOT_IN_ROOM`、`CLIENT_INTENT_ID_REQUIRED`、`MATCH_NOT_STARTED`、`INVALID_RECONNECT_TOKEN`、`PHASE_NOT_ALLOWED` 和 command 层拒绝。
- `JoinRoom` 返回 `PlayerSessionDto`，其中包含当前 reconnect token；生产路径只把 token hash 写入 `match_players`，不保存明文。
- 如果 PDF/FAQ 与旧 Java fixture 冲突，以五份官方 PDF/FAQ 和官网卡面为准，fixture 的 `expected` 应更新，旧输出保留在 `legacyOracle`。
