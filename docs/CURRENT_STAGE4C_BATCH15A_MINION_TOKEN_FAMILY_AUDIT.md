# 阶段 4C-15A Minion Token Family 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-15A token subtype / family / minion classification 最小前置模型的规则证据与 P0/P1 审计口径。B 本批只完成模型前置切片，不实现 Viktor destroyed non-minion trigger 本体。D 本轮只更新文档，不修改服务端、前端、E 覆盖矩阵或 `riftbound-dotnet.sln`。

## 1. 4C-15A 关闭的子项

4C-15A 关闭的是 4C-15 blocker 中“没有稳定随从 token family 标记”的前置模型子项之一：

- 新增稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- `P6TokenFactoryCatalog` 中官方三种“随从”token factory（`OGN·271/298`、`OGN·272/298`、`OGN·273/298`）带该 tag。
- `CoreRuleEngine.CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
- Viktor legend 直接创建随从路径同步带 `TOKEN_FAMILY:MINION`。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield held minion 等路径可生成带 marker 的随从 token。
- 普通单位不带 marker；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”token factory 不带 marker。
- hidden face-down standby 即使内部带 marker，对手 snapshot 仍不泄漏 tags / cardNo / power。

## 2. 明确未关闭

- Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 本体未实现。
- destroy / cleanup 入队时 destroyed target pre-removal state 判定未关闭。
- 完整 trigger engine 未关闭。
- 1009 / 811 full-official 未关闭。
- FAQ regression 未关闭。
- 正式 18-step E2E 未关闭。

本批未改协议 record 字段，未改前端，不宣称 READY / READY-CANDIDATE。

## 3. 验证记录

A 独立复核：

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：passed，3375/3375。
- `git diff --check`：passed。

## 4. 后续建议

- B 后续实现 Viktor 前，应先明确 destroyed target pre-removal state 来源：摧毁对象进入墓地 / 移除前，其 token family / subtype / controller / face-up 等状态如何被 trigger enqueue 使用。
- D/E 后续需要把 `TOKEN_FAMILY:MINION` 与官方“随从”卡牌 / token factory 证据继续映射，避免把所有 `CARD_TYPE:UNIT` token 当成随从。
- C 暂不需要前端改动；前端只能展示服务端 snapshot / event，不本地推断 token family。

项目仍 **NOT READY**：4C-15A 只部分关闭 token classification 前置 blocker，不关闭 Viktor trigger 本体、完整 trigger engine、1009 / 811 full-official、FAQ regression 或最终正式 E2E。
