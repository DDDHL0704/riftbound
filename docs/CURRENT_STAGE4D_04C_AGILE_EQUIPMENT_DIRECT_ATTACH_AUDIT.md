# Stage 4D-04C Agile Equipment Direct Attach Audit

日期：2026-05-15
结论：**ACCEPTED / PROJECT NOT READY**

本审计记录 4D-04C Agile equipment direct-play attach representative 的 A 侧验收结果。B-Implementation / Singer `019e2b7e-8eed-7803-b03a-ab9bf538171c` 已完成窄实现，A 已按 handoff 验收 focused、adjacent、keyword guard、historical fixture migration 与 backend full test。该批只减少 P1-002 equipment keyword residual，不关闭完整装备规则、LayerEngine、card matrix、frontend final validation 或 READY。

## 1. Scope

4D-04C 只覆盖印刷 `灵便` 装备从手牌 `PLAY_CARD` 时的 direct attach representative：

- `SFD·022/221` 长剑
- `SFD·056/221` 斯特拉克的挑战护手
- `SFD·064/221` 布甲
- `SFD·186/221` 旋转飞斧

服务端现在要求这些装备从手牌打出时选择一个己方单位目标。合法命令保留既有支付 / stack 行为，结算后装备对象的 `AttachedToObjectId` 指向该单位，并发出 `EQUIPMENT_ATTACHED`，reason 为 `AGILE_DIRECT_PLAY_ATTACH`。缺失目标、敌方单位、非单位、非法 public-zone / stale / wrong-controller target 会 no-mutation reject。

## 2. Accepted Changes

- `CardEquipmentKeywordRules` 新增 printed Agile direct-play attach representative set，并在 profile 上暴露 `HasImplementedRepresentativeAgileDirectPlayAttachBoundary`；report 仍保留 reaction timing、Jax-granted Agile、ephemeral/static breadth、Tempered optional attachment、weapon/static modifiers、copy-text effects、owner/controller changes、attach lifecycle 与 full official residual。
- `CoreRuleEngine` 将代表性 Agile equipment 的 `PLAY_CARD` target scope 映射到 friendly unit，并在 stack resolution 时把 equipment attach 到 selected unit。
- `MatchSession` 的 prompt/source requirement/target choice shape 已向客户端公开 Agile equipment friendly-unit target。
- Focused tests 新增 printed Agile equipment legal attach、prompt metadata、missing / invalid target no-mutation guard。
- 既有 Agile fixtures 从旧 no-target 成功/拒绝口径迁移为 direct attach 成功与 missing-target reject；Arena Service Crew / P79 adjacent fixtures 已补合法 target，避免旧 fixture 把新 target requirement 误判为回归。

## 3. A-Side Validation

完整命令与输出摘要见 `docs/CURRENT_STAGE4D_04C_AGILE_EQUIPMENT_DIRECT_ATTACH_EVIDENCE.md`。A 侧已验证：

- focused / migrated baseline：57/57 passed
- rejected / shape guards：113/113 passed
- adjacent equipment / historical focused：207/207 passed
- keyword guard：17/17 passed
- historical P79 / Arena Service Crew targeted recheck：6/6 passed
- backend full：4368/4368 passed
- `git diff --check` passed

## 4. Residual Risks

P1-002 仍 open。4D-04C 不实现或不关闭：

- Agile reaction timing / non-main-window attachment breadth
- Jax-granted Agile and non-printed Agile
- ephemeral destruction timing beyond representative fixture expectations
- Tempered optional attachment
- weapon/static power modifiers and continuous effects
- copy-text effects
- owner/controller changes and full attach lifecycle breadth
- LayerEngine
- card matrix full-official upgrade
- frontend final build / smoke / formal E2E fresh-run
- final READY

## 5. Verdict

4D-04C is accepted as a narrow server-authoritative Agile direct-play attach representative. The project remains **NOT READY** and the active goal must not be marked complete.
