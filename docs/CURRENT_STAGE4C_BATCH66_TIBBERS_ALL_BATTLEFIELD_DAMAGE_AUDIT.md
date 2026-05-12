# Stage 4C-66 Tibbers All Battlefield Damage Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-c168bd394c`
- 代表卡：提伯斯 / Tibbers `OGS·018/024` / cardId `31597`
- 代表 effect：`TIBBERS_PLAY_UNIT_DAMAGE_ALL_BATTLEFIELD_UNITS_3`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 8 mana、0 目标入栈、stack pass-pass 后源牌进入基地成为 7 战力单位，并对公开战场单位造成 3 点伤害。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGS·018/024` 为 direct card behavior：`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 7`、`DamagesAllBattlefieldUnits: true`、`DamageAmount: 3`。
- `p2-preflight-play-tibbers-damage-all-battlefield-units.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `ConformanceFixtureRunnerTests` 既有 fixture、非单位战场对象跳过测试、unexpected target rejection 测试覆盖该路线。

## 验证

- focused：3/3 passed。
- battlefield damage regression：63/63 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 all battlefield-wide damage cards / alternate damage amounts、damage prevention / replacement / cleanup、lethal-damage trigger interactions、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
