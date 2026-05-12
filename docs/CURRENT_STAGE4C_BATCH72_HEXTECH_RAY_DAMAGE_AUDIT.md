# Stage 4C-72 Hextech Ray Damage Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-441cb9fb7f`
- 代表卡：海克斯射线 / Hextech Ray `OGN·009/298` / cardId `31215`
- 代表 effect：`HEXTECH_RAY_DAMAGE_3`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付 1 mana、选择一名战场单位、stack pass-pass 后造成 3 点伤害并进入废牌堆，同时记录 end-turn damage cleanup 与 Swift spell-duel focus 代表路径。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·009/298` 为 direct card behavior：`Cost: 1`、`TargetCount: 1`、`DamageAmount: 3`、`CanPlayDuringSpellDuel: true`。
- `p2-preflight-play-hextech-ray-damage-stack.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json` 已记录伤害在 `END_TURN` 特殊清理中移除并推进到下一回合。
- `p6-play-swift-hextech-ray-in-spell-duel-focus.fixture.json` 已记录 Swift 法术对决焦点窗口打出与结算后焦点移交。
- `CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit` 已记录基地单位目标拒绝 no-mutation guard。

## 验证

- focused Hextech Ray regression：4/4 passed。
- damage / Swift / cleanup regression：202/202 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明完整 `BREAK-JFAQ-260416` / `JFAQ-251023` / `SOUL-JFAQ-260114` FAQ 裁定、完整 FEPR target / stack lifecycle、全部 timing windows、完整 spell-duel lifecycle、battle damage assignment、PaymentEngine beyond ordinary pay 1、damage prevention / replacement / lethal cleanup / trigger matrix、hidden-info / redaction matrix、全部 battlefield-unit target-scope permutations、1009/811 full-official 或 formal 18-step E2E 已完成。
