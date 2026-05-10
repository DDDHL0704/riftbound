# 阶段 4C-25 Icevale Archer Attack Payment 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Icevale Archer / 冰谷弓箭手 `UNL-065/219`、`FU-c170628e3a` 的代表性 attack -> trigger payment -> temporary power -1 证据。4C-25 不宣称 full-official，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-25 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`UNL-065/219` Icevale Archer / 冰谷弓箭手 | 只取“当我进攻时，可支付 1，使此处一名单位本回合 -1”作为代表路径。 |
| Battle / attack timing | `CORE-260330` p33-p35 rules 333-340；p77-p78 rules 454-464；`JFAQ-251023` p2-p4 q2.2-q2.4 | 本批使用现有 active start-battle / declare battle representative task 作为 attack trigger 来源。 |
| Trigger payment | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | `TRIGGER_PAYMENT` / `PAY_COST` 代表 payment window，覆盖 pay / decline。 |
| Target / same battlefield | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标；invalid target 不触发。 |
| Temporary modifier | `CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324 | 支付成功后目标获得本回合 power -1。 |
| Hidden information | `CORE-260330` p4-p8 rules 107-129 | hidden / face-down / standby / opponent-controlled source 不触发、不泄漏。 |

## 实现证据

- Representative route：active start-battle task 下，visible face-up Icevale source 被声明为攻击者，且 `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的 visible face-up unit target 后，服务端打开 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- Pay route：`PAY_COST(SPEND_MANA:1)` 后，预选目标获得 `POWER_MODIFIED_UNTIL_END_OF_TURN:-1` 代表修正。
- Decline route：`PAY_COST(DECLINE)` 关闭支付窗口，目标不获得 -1，状态不发生目标战力 mutation。
- Guard route：invalid target、hidden / face-down / standby / opponent-controlled source 不产生 trigger payment prompt，不泄漏 hidden source metadata，不修改目标战力。
- Boundary：本批不覆盖 full attack-trigger family、完整 target selection prompt、支付后恢复战斗时点、Spellshield target tax、LayerEngine、full PaymentEngine、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Icevale|FullyQualifiedName~AttackPayment|FullyQualifiedName~TriggerPayment|FullyQualifiedName~DeclareBattle|FullyQualifiedName~Vayne|FullyQualifiedName~Lux"` 通过 102/102。

## 仍未关闭

- 完整 spell duel / battle lifecycle、start-battle task、battle response window、damage assignment 与支付后恢复战斗时点。
- 完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- 完整 trigger engine、complete APNAP、trigger batch、optional trigger handling、attack-trigger family 与完整 effect resolution。
- 完整 target selection prompt、same-battlefield target matrix、target invalidation、Spellshield target tax。
- 完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration matrix。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- UI/DTO 解释字段、战斗恢复 UX、event label / replay redaction。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
