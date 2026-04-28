# 协议语义边界

更新时间：2026-04-28

## 1. 目的

本文件记录客户端提交意图和服务端权威结算之间的命令语义。它优先服务 P1 联机底座，避免继续继承旧 Java 中 `PASS -> TURN_ENDED` 的混淆。

规则依据见 `docs/rules-evidence-index.md`。当前三条 Java fixture 仍是 `legacyOracle`，不是最终规则裁决。

## 2. 让过与结束回合

| cmdType | 语义 | 规则语境 | 当前实现状态 |
|---|---|---|---|
| `PASS_PRIORITY` | 让过优先行动权 | FEPR/结算链闭环，依据 `CORE-260330` p34-p35 rules 335-340 | 协议 DTO 已保留；规则引擎待实现。 |
| `PASS_FOCUS` | 让过焦点 | 法术对决开环，依据 `CORE-260330` p36 rules 347-348 | 协议 DTO 已保留；规则引擎待实现。 |
| `END_TURN` | 表明主阶段没有要执行的自决行动，进入回合结束流程 | 普通开环主阶段，依据 `CORE-260330` p30 rules 316.1-316.6 和 p30-p31 rules 317.1-317.3 | 旧占位实现已能产出粗粒度事件，后续需拆清特殊清理、下一回合开始、召出和抽牌。 |
| `PASS` | 旧 Java 兼容命令 | 不作为最终规则语义 | 仅保留给 legacy fixture 和旧客户端兼容；新 fixture 不应再新增裸 `PASS`。 |

## 3. 实现约束

- 客户端只提交意图，不直接修改状态。
- `ActionPrompt` 后续应按当前回合状态暴露 `PASS_PRIORITY`、`PASS_FOCUS` 或 `END_TURN`，避免同时暴露语义冲突的动作。
- `PASS` 在 P1 期间只能用于 legacy fixture；若新测试需要让过，必须使用更具体的 command。
- 如果 PDF/FAQ 与旧 Java fixture 冲突，以五份官方 PDF/FAQ 和官网卡面为准，fixture 的 `expected` 应更新，旧输出保留在 `legacyOracle`。
