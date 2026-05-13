# Stage 4C-95 Static Effect Design Gate

审计日期：2026-05-13
结论：**不能 evidence-only 入账；项目整体仍 NOT READY。**

## 范围

本批只记录剩余 direct-card `IMPLEMENTED_UNTESTED` 候选中，当前已有 fixture 只覆盖普通 source-unit 入场，但官方核心效果仍显式 deferred 的四个 FU：

- `FU-0973164d07` / `OGN·011/298` / 熔浆巨龙 / `OGN_MOLTEN_DRAKE_ACTIVE_ENTRY_PLAY_UNIT`
- `FU-c9bce10c0e` / `OGN·073/298` / 娑娜 / `OGN_SONA_END_TURN_READY_RUNES_PLAY_UNIT`
- `FU-430074702b` / `OGS·001/024` / 安妮 / `OGS_ANNIE_DAMAGE_PLUS_STATIC_PLAY_UNIT`
- `FU-af793555bb` / `UNL-104/219` / 温驯的宝石龙 / `GENTLE_GEM_DRAGON_READY_RUNES_STATIC`

## 判定

这些 FU 均不适合按 Stage 4C representative evidence-only 关闭：

- `CardBehaviorRegistry` 目前只登记普通 `PLAY_CARD` source-unit-to-base 参数：费用、0 目标、入场战力与少量标签。
- 对应 fixture 描述和 `rules-evidence-index.md` 均明确写着核心卡面效果暂缓。
- 现有测试可证明“从手牌打出并成为单位对象”和“带目标拒绝”，但不能证明官方静态/触发/伤害修正文本已经实现。
- 因此本批不更新 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`，不将 `stage4B.freezeStatus` 升级为 `IMPLEMENTED_TESTED`。

## 缺口

- 熔浆巨龙：在场上时其他友方单位以活跃状态进场。需要服务端在单位入场事件或 destination resolution 中接入静态替代/修正，并验证非友方、非单位、来源离场、战场/基地/待命等边界。
- 娑娜：回合结束时若位于战场上，让四枚友方符文变为活跃。需要服务端 end-turn trigger / static check、战场位置判断、最多四枚符文选择或确定性策略、ActionPrompt / no prompt 口径和多玩家边界。
- 安妮：控制者法术和技能造成的每段伤害增加 1。需要 damage layer / modifier 模型，区分法术、技能、战斗伤害、范围伤害、多段伤害、控制权和来源状态。
- 温驯的宝石龙：当你打出我或其他龙属性单位时，让最多两枚符文变为活跃。需要龙单位打出触发、符文目标域、最多两枚选择、目标合法性、无目标/少目标分支与 prompt metadata。

## 证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-molten-drake-active-entry-static.fixture.json`：描述明确写明 friendly active-entry static text deferred。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-sona-ready-runes-static.fixture.json`：描述明确写明 end-turn rune readying text deferred。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogs-annie-damage-plus-static.fixture.json`：描述明确写明 spell and skill damage modifier text deferred。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-gentle-gem-dragon-rune-ready-static.fixture.json`：描述明确写明 rune-ready trigger deferred。
- `docs/rules-evidence-index.md` 对上述四个 fixture 的条目也只记录普通入场路径，并把官方核心效果列为暂缓。

## 口径

本批是 design gate，不修改功能代码、测试代码、前端代码或 coverage matrix。上述四个 FU 仍保持 `IMPLEMENTED_UNTESTED` / `NEEDS_AUTOMATED_TEST_EVIDENCE`，等待后续功能设计、服务端实现、prompt 契约、测试与正式 Stage 4C evidence 入账。
