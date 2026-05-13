# Stage 4C-94 Babbling Poro Predict Evidence Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-677c27eea7`
- 代表卡：叨叨魄罗 / Babbling Poro `UNL-224/219` / cardId `34777`
- 代表 effect：`UNL_BABBLING_PORO_PLAY_UNIT_PREDICT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付基础 2 费用、选择控制者主牌堆顶部牌、stack / pass-pass 后源牌进入控制者基地成为 2 战力、带 `CARD_TYPE:UNIT|魄罗|预知` 标签的单位对象，并将所选顶部牌回收到主牌堆底部。
- 本批同步记录选择非顶部牌的 target rejection，防止 predict 查看窗口被任意主牌堆目标驱动变异。
- 本批不声明 source-unit predict 的不回收 / decline 分支、完整主牌堆查看窗口隐私矩阵、非手牌或替代来源打出、完整 PaymentEngine、完整 FEPR target / stack / timing windows、hidden-info / redaction、1009/811 full-official 或 final READY 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `UNL-224/219` 为 direct card behavior：费用 2、`FriendlyMainDeckCard` target scope、`MinTargetCount: 0`、`MainDeckLookCount: 1`、`RecyclesSelectedMainDeckTargets: true`、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 2`、`SourceUnitTags: 魄罗|预知`。
- `p2-preflight-play-unl-babbling-poro-predict-recycle.fixture.json` 覆盖普通主阶段从手牌打出：支付 2、选择 P1 主牌堆顶部一张牌、入栈、双方让过后源牌进入 P1 基地，并写入 `CARDS_RECYCLED` 将所选顶部牌移到主牌堆底部。
- `CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard` 覆盖 UNL 叨叨魄罗代表 fixture，并同时覆盖同族 predict source-unit 成功路径。
- `CoreRuleEngineRejectsPredictSourceUnitWhenTargetIsOutsideTopCard` 覆盖选择第二张主牌堆牌时拒绝；拒绝后费用、手牌、主牌堆、stack 与 tick 不发生变更。

## 验证

- focused predict source-unit regression：12/12 passed。
- adjacent predict / source-unit / target / stack / priority / payment regression：1830/1830 passed。
- backend full：3771/3771 passed。

## 非覆盖

不声明 source-unit predict 的不回收 / decline 分支、完整主牌堆查看窗口隐私矩阵、非手牌或替代来源打出、完整 PaymentEngine、完整 FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 final READY 已完成。
