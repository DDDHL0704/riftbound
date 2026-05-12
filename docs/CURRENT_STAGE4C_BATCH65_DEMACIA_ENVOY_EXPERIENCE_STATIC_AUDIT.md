# Stage 4C-65 Demacia Envoy Experience Static Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-d68c203b01`
- 代表卡：德玛西亚使节 / Demacia Envoy `UNL-092/219` / cardId `34630`
- 代表 effect：`DEMACIA_ENVOY_PLAY_UNIT_GAIN_EXPERIENCE_STATIC`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 2 mana、0 目标入栈、stack pass-pass 后源牌进入基地成为 2 战力单位、控制者获得 1 经验，并记录 gained-experience-this-turn marker。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-092/219` 为 direct card behavior：`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 2`、`GainExperienceOnPlay: 1`。
- `p2-preflight-play-demacia-envoy-experience-static.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `ConformanceFixtureRunnerTests` 既有 fixture 与 P4 fixed experience test 覆盖该路线。

## 验证

- focused：4/4 passed。
- experience regression：37/37 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 all experience cards / amounts、experience payment / optional cost replacement、level-up LayerEngine、battlefield conquest / hold Hunt experience、activate ability experience gain、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
