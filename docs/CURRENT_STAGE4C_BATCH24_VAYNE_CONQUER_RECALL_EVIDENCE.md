# 阶段 4C-24 Vayne Conquer Recall 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Vayne / 薇恩 `OGN·035/298`、`FU-c027639a3c` 的代表性 conquer -> trigger payment -> return owner hand 证据。4C-24 不宣称 full-official，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-24 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`OGN·035/298` Vayne / 薇恩 | 只取“每当我征服一处战场时，你可以选择支付 1 来让我返回所属的手牌”作为代表路径；`强攻3` 与 active-entry 暂缓。 |
| Conquer / battlefield lifecycle | `CORE-260330` p77-p78 rules 454-464；`JFAQ-251023` p6-p7 q5.1-q5.4 | 本批使用现有 battlefield conquer representative event 作为触发来源。 |
| Trigger payment | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | `TRIGGER_PAYMENT` / `PAY_COST` 代表 payment window，覆盖 pay / decline。 |
| Zone / hand movement | `CORE-260330` p4-p8 rules 107-129；p57-p59 rules 413-416 | 支付成功后 Vayne 返回 owner hand。 |
| Hidden information | `CORE-260330` p4-p8 rules 107-129 | hidden / face-down / standby / opponent-controlled source 不触发、不泄漏。 |

## 实现证据

- Vayne representative route：visible face-up Vayne source 征服战场后打开 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- Pay route：`PAY_COST(SPEND_MANA:1)` 后，Vayne 从场上 / 基地对象位置返回 owner hand。
- Decline route：`PAY_COST(DECLINE)` 关闭支付窗口，Vayne 不回手，状态不发生回手 mutation。
- Guard route：hidden / face-down / standby / opponent-controlled source 不产生 trigger payment prompt，不泄漏 hidden source metadata，不移动 Vayne。
- Boundary：本批不覆盖 full Assault3、active-entry、complete conquer/control-zone matrix、full PaymentEngine、owner/controller 复杂变化或 hand visibility / replay redaction 全矩阵。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Vayne|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"` 通过 52/52。

## 仍未关闭

- 完整 battlefield / control / conquer lifecycle、control freeze/release、held/conquer scoring order 与 battle cleanup 全矩阵。
- 完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- 完整 trigger engine、complete APNAP、trigger batch、optional trigger handling 与完整 effect resolution。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- Vayne Assault3 combat modifier、active-entry condition、owner/controller return-hand matrix、hand visibility / replay redaction。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
