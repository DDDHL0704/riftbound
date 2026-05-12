# Stage 4C-71 Diana Spell Duel Static Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-4215291160`
- 代表卡：黛安娜 / Diana `UNL-079/219` / cardId `34612`
- 代表 effect：`DIANA_SPELL_DUEL_INSIGHT_STATIC`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 3 mana、0 目标入栈、stack pass-pass 后源牌进入基地成为 3 战力 `CARD_TYPE:UNIT|巨神峰` 单位。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-079/219` 为 direct card behavior：`Cost: 3`、`TargetCount: 0`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 3`、`SourceUnitTags: 巨神峰`。
- `p2-preflight-play-diana-spell-duel-static.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `CoreRuleEnginePlaysKeywordOnlySourceUnit` 的参数化覆盖包含 `UNL-079/219` 和 `UNL-079a/219`，确认 Diana spell-duel-static 入场路线。
- `CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided` 的参数化覆盖包含 Diana 带目标拒绝路线。
- `UNL-079a/219` Diana alt A 本批只作为相邻回归，不贴 `stage4C71` overlay。

## 验证

- keyword-source / rejection regression：459/459 passed。
- Spell duel / insight adjacent regression：45/45 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 Diana 的实际法术对决开始触发、Insight 支付 / 展示 / 抽取 / 回收、`UNL-079a/219` alt A FU 覆盖、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、LayerEngine、hidden-info / top-card redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
