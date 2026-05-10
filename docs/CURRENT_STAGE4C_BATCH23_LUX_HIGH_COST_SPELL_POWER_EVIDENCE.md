# 阶段 4C-23 Lux High-Cost Spell Power 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Lux / 拉克丝 `OGS·006/024`、`FU-f18a49e06d` 的代表性 high-cost spell trigger -> temporary power 证据。4C-23 不宣称 full-official，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-23 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`OGS·006/024` Lux / 拉克丝 | 控制者打出费用不低于 5 的法术时，Lux 本回合战力 +3。 |
| Spell play / stack | `CORE-260330` p39-p42 rules 355-356；p33-p35 rules 333-340 | 本批沿用现有 `PLAY_CARD` -> priority/stack 的 spell play 入口。 |
| Temporary power | `CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324 | 以 `POWER_MODIFIED_UNTIL_END_OF_TURN` 和 `UntilEndOfTurnPowerModifier` 代表本回合战力修正。 |
| Hidden information | `CORE-260330` p4-p8 rules 107-129 | face-down / standby / invalid source 不触发、不泄漏。 |

## 实现证据

- Lux representative route：visible face-up `OGS·006/024` Lux 在 controller base/field，controller 成功打出 cost >= 5 spell 后记录 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events 与 `POWER_MODIFIED_UNTIL_END_OF_TURN`。
- Result state：Lux power 从 5 到 8，`UntilEndOfTurnPowerModifier` 从 0 到 3。
- Guard route：cost < 5 spell、opponent spell、face-down Lux、standby Lux、source not on field 都不产生 Lux trigger，也不修改 Lux power。
- Boundary：本批使用现有代表路径，不关闭完整 `TriggerInstance` batch、optional trigger、PaymentEngine paid-cost override 或 LayerEngine full matrix。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCostSpell|FullyQualifiedName~Ravenbloom|FullyQualifiedName~RealTriggerQueue"` 通过 67/67。
- Test evidence：`RealTriggerQueueTests.LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn`、`LuxLowCostSpellDoesNotTrigger`、`LuxOpponentHighCostSpellDoesNotTrigger`、`LuxHiddenOrStandbyHighCostSpellDoesNotTrigger`、`LuxInvalidSourceNotOnFieldDoesNotTrigger`。

## 仍未关闭

- 完整 trigger engine、complete APNAP、trigger batch、optional trigger handling 与完整 effect resolution。
- 完整 PaymentEngine、paid-cost override、增减费、额外费用、替代费用 full matrix。
- LayerEngine、timestamp/dependency、所有 temporary modifier cleanup duration。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
