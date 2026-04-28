# 协议语义边界

更新时间：2026-04-28

## 1. 目的

本文件记录客户端提交意图和服务端权威结算之间的命令语义。它优先服务 P1 联机底座，避免继续继承旧 Java 中 `PASS -> TURN_ENDED` 的混淆。

规则依据见 `docs/rules-evidence-index.md`。当前三条 Java fixture 仍是 `legacyOracle`，不是最终规则裁决。

## 2. 房间生命周期

| cmdType / Hub 方法 | 语义 | 当前实现状态 |
|---|---|---|
| `JoinRoom` | 玩家入座，前两名玩家获得稳定 `P1` / `P2` seat | 已实现；第三名玩家返回 `ROOM_FULL`。 |
| `READY` / `GameHub.Ready` | 玩家确认准备；双方都 Ready 后房间进入 `IN_PROGRESS` | 已实现；第一名 Ready 广播 `READY` + `PLAYER_READY`，第二名 Ready 广播 `START` + `MATCH_STARTED`。 |
| `Reconnect` | 通过 reconnect token 恢复玩家连接 | 已实现；服务端只持久化 `sha256:` token hash，重连成功轮换 token/hash。 |

状态流：`EMPTY -> SEATING -> IN_PROGRESS`，`FINISHED` 作为后续胜负结算状态保留。未进入 `IN_PROGRESS` 前提交规则命令会返回 `MATCH_NOT_STARTED`。

## 3. 让过与结束回合

| cmdType | 语义 | 规则语境 | 当前实现状态 |
|---|---|---|---|
| `PASS_PRIORITY` | 让过优先行动权 | FEPR/结算链闭环，依据 `CORE-260330` p34-p35 rules 335-340 | 协议 DTO 已保留；规则引擎待实现。 |
| `PASS_FOCUS` | 让过焦点 | 法术对决开环，依据 `CORE-260330` p36 rules 347-348 | 协议 DTO 已保留；规则引擎待实现。 |
| `END_TURN` | 表明主阶段没有要执行的自决行动，进入回合结束流程 | 普通开环主阶段，依据 `CORE-260330` p30 rules 316.1-316.6 和 p30-p31 rules 317.1-317.3 | 旧占位实现已能产出粗粒度事件，后续需拆清特殊清理、下一回合开始、召出和抽牌。 |
| `PASS` | 旧 Java 兼容命令 | 不作为最终规则语义 | 仅保留给 legacy fixture 和旧客户端兼容；新 fixture 不应再新增裸 `PASS`。 |

## 4. 实现约束

- 客户端只提交意图，不直接修改状态。
- `ActionPrompt` 后续应按当前回合状态暴露 `PASS_PRIORITY`、`PASS_FOCUS` 或 `END_TURN`，避免同时暴露语义冲突的动作。
- `PASS` 在 P1 期间只能用于 legacy fixture；若新测试需要让过，必须使用更具体的 command。
- Hub 错误统一返回 `ErrorDto(code, message)`；当前已覆盖 `ROOM_FULL`、`PLAYER_NOT_IN_ROOM`、`MATCH_NOT_STARTED`、`INVALID_RECONNECT_TOKEN` 和 command 层拒绝。
- `JoinRoom` 返回 `PlayerSessionDto`，其中包含当前 reconnect token；生产路径只把 token hash 写入 `match_players`，不保存明文。
- 如果 PDF/FAQ 与旧 Java fixture 冲突，以五份官方 PDF/FAQ 和官网卡面为准，fixture 的 `expected` 应更新，旧输出保留在 `legacyOracle`。
