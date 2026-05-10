# 阶段 4C-15 Viktor Feasibility Blocker 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对阶段 4C-15 Viktor destroyed non-minion token trigger 候选项的只读可行性审计口径。B 本轮只做可行性检查，未修改代码，未新增测试。D 本轮只更新文档，不修改服务端、前端、E 覆盖矩阵或 `riftbound-dotnet.sln`。

## 1. 候选范围

- 批次：4C-15 Viktor feasibility blocker。
- 候选 FU：`FU-b5cb36a5c9`。
- 候选语义：Viktor destroyed non-minion token trigger。
- 4C-14 Savage Jawfish 已 checkpoint：`2deef64 checkpoint: complete stage 4C savage jawfish trigger batch`。

## 2. 阻断原因

当前不建议硬编码 Viktor 的“非随从”判定，原因是服务端对象模型缺少稳定分类来源：

- `CardObjectTags` 没有 `Minion` / `随从` / subtype 字段。
- `CardObjectState` 没有稳定 token family / subtype / `isMinion` 字段。
- 多个“随从”创建路径经 `CreateBaseUnitTokens` 只落成 `CARD_TYPE:UNIT`，不保留 `cardNo` / `tokenName` / `TokenFamilyName`。
- 摧毁事件发生时，runtime 无法可靠区分“随从单位”和普通单位。
- 当前 Viktor fixtures 也仍描述 destroyed-listener / non-minion filtering / minion-token path deferred。

## 3. 4C-15 审计结论

- 4C-15 未实现。
- 4C-15 未新增测试。
- 4C-15 不关闭 Viktor `FU-b5cb36a5c9`。
- 4C-15 只把 Viktor destroyed non-minion token trigger 记录为 P0/P1 blocker。
- 在 token subtype / family 模型冻结前，硬编码“非随从”会产生规则风险和未来迁移成本。

## 4. 后续建议

推荐二选一：

- 先做模型前置切片：冻结 `CardObjectState` subtype / token-family / `isMinion` 语义，并统一随从 token factory 写入，再回到 Viktor。
- 由用户确认跳过 Viktor，改做不依赖“非随从”分类的下一个 safe FU。

A 推荐路径：先做模型前置切片。理由是 Viktor 的“非随从”过滤属于规则分类基础设施，不是单卡局部条件；若跳过该模型直接改做别的 FU，Viktor、随从 token、token family、后续 trigger / replacement / FAQ 回归仍会在阶段 4 后段重新阻塞。

用户裁定点：

- 是否允许阶段 4C-15A 新增服务端对象分类字段，例如 `CardObjectState` 上的 token family / subtype / minion-classification 只读事实。
- 是否允许统一更新现有随从 token factory，使 runtime 摧毁事件能稳定判断 destroyed unit 是否为“随从”。
- 若暂不做模型切片，是否允许 A 跳过 Viktor，把 4C 下一批切到不依赖“非随从”分类的 safe FU。

在上述裁定前，A 不应派发 Viktor 功能实现任务。

## 5. 4C-15 当时仍保留 P0/P1

- P0：Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger 仍未实现。
- P0：token subtype / token-family / minion classification 模型未冻结。4C-15A 后该模型前置项已被 `TOKEN_FAMILY:MINION` 最小切片部分关闭，但本条保留为 4C-15 blocker 历史。
- P0：完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs。
- P0：Kogmaw / Karthus / Undercover Agent 等后续触发族。
- P0：hidden / face-down 原始触发建模、完整 effect resolution、FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：Viktor fixtures 的 destroyed-listener / non-minion filtering / minion-token path 仍 deferred。

项目仍 **NOT READY**，不得标记 READY / READY-CANDIDATE；本 blocker 不代表 1009 / 811 full-official 或正式 E2E。

## 6. 阶段 4C-15A 后续记录

4C-15A 已由 B 完成，不实现 Viktor 本体，只做 token subtype / family / minion classification 最小前置模型。细化审计见 `docs/CURRENT_STAGE4C_BATCH15A_MINION_TOKEN_FAMILY_AUDIT.md`。

4C-15A 已部分关闭 4C-15 blocker 中的模型前置子项：

- 新增稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- `P6TokenFactoryCatalog` 的官方三种“随从”token factory（`OGN·271/298`、`OGN·272/298`、`OGN·273/298`）带该 tag。
- `CoreRuleEngine.CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
- Viktor legend 直接创建随从路径同步带 `TOKEN_FAMILY:MINION`。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield held minion 等路径可生成带 marker 的随从 token。
- 普通单位不带 marker；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”token factory 不带 marker。
- hidden face-down standby 即使内部带 marker，对手 snapshot 仍不泄漏 tags / cardNo / power。

4C-15A 验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3375/3375。
- `git diff --check` passed。

4C-15A 后仍保留：

- Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 本体。
- destroy / cleanup 入队时 destroyed target pre-removal state 判定。
- 完整 trigger engine。
- 1009 / 811 full-official。
- FAQ regression。
- 正式 18-step E2E。

4C-15A 未改协议 record 字段，未改前端，未实现 Viktor trigger。项目仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。
