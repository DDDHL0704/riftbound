# Stage 4C-15A Minion Token Family Evidence

更新时间：2026-05-10

结论：**NOT READY；4C-15A 是 model / infrastructure overlay，不新增 card effect full-official，不实现 Viktor 本体，不允许 1009/811 批量实现。**

## Source Boundary

本文件只记录 E 覆盖矩阵 / FAQ evidence / functional unit 侧事实。E 不修改功能代码、前端、D checkpoint、server audit、rules index 或 `riftbound-dotnet.sln`。

官方数据仍使用 `data/official` 中 2026-04-27 固定快照：

- frozen snapshot entries：1009
- frozen functional units：811
- cardId / collector no / oracle effectId 口径不变

## 4C-15A Model Facts

新增稳定 marker：

- `TOKEN_FAMILY:MINION`
- `CardObjectTags.MinionTokenFamily`

已记录的 infrastructure 行为：

- 官方随从 token factory `OGN·271/298`、`OGN·272/298`、`OGN·273/298` 带 Minion family marker。
- `CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
- Viktor legend 直接随从创建也同步使用该 marker。

## Verified Positive Evidence

以下随从 token 创建路径带 marker：

- Common Cause / `OGS·015/024`
- Future Forge / `OGN·212/298`
- Faithful Craftsman / `OGN·211/298`
- Vanguard Captain / `OGN·218/298`
- Mechanical Trickster / `OGN·239/298`
- Viktor legend / Arcane Herald functional unit
- battlefield-held minion creation
- official Minion token factories `OGN·271/298`、`OGN·272/298`、`OGN·273/298`

## Negative / Visibility Guards

- 普通单位不带 `TOKEN_FAMILY:MINION`。
- Gold / Sprite / Warhawk / Sand Soldier 等非“随从”不带 `TOKEN_FAMILY:MINION`。
- hidden face-down standby opponent snapshot 不泄漏 tags / cardNo / power。

## Viktor Boundary

4C-15A 可以降低或关闭 token subtype / family / minion-classification 前置 blocker，但不能关闭 Viktor trigger：

- `FU-b5cb36a5c9` Viktor 仍为 `NEEDS_ENGINE_SUPPORT` / `SHARED_ORACLE_IMPLEMENTATION`。
- `FU-b5cb36a5c9` 仍 `fullOfficial=false`。
- 4C-15A 不实现 Viktor 本体，不授予 `full-official-rule-pass`。

## Test Evidence

A 独立复核结果：

- backend full：3375/3375 passed
- `git diff --check`：passed

## Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层：`stage4CBatch15AMinionTokenFamilyModel`

本批不新增 per-FU full-official 标记。

## Counts

- frozen snapshot entries：1009
- frozen functional units：811
- `stage4C15AVerifiedInfrastructure`：true
- `stage4C15AFullOfficialFunctionalUnits`：0
- `stage4C15AFullOfficialSnapshotEntries`：0
- `stage4C15AFUOverlayTags`：0
- fullOfficialUpgrades：0

## Still Missing P0/P1

- Viktor destroyed non-minion trigger behavior 仍未由 4C-15A 实现或 full-official。
- complete trigger engine beyond visible verified slices。
- same-source / same-pass / multi-destroy multiplicity and non-minion classification in real trigger contexts。
- hidden / face-down original visibility modeling beyond tested snapshot redaction guards。
- Kogmaw / Karthus / Undercover Agent 未覆盖。
- FAQ adjudication and regression tests。
- 1009 snapshot-entry / 811 functional-unit full-official coverage。
- formal 18-step E2E。

是否允许进入 1009 张卡批量 full-official 覆盖：**不允许。**
