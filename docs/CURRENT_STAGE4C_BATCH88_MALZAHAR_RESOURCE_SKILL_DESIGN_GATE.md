# Stage 4C-88 Malzahar Resource Skill Design Gate

审计日期：2026-05-13
结论：**进入 design gate；不可 evidence-only 入账；项目整体仍 NOT READY。**

## 范围

- 候选 FU：`FU-0f7cbe26ce`
- 候选卡：玛尔扎哈 / Malzahar `OGN·113/298` / cardId `31332`
- 候选 effect：`OGN_MALZAHAR_TAP_RUNE_GAIN_PLAY_UNIT`
- 官方文本：摧毁一个友方单位或装备，横置；迅捷获得 `A A`，用以支付符能费用；可在己方回合或法术对决中打出；获得费用资源的技能无法成为其他法术的反应目标。
- 本批只做 A 主控 design-gate / 派单规格，不修改服务端、前端、测试或覆盖矩阵。

## 已有事实

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 目前只登记 `OGN·113/298` 的普通打出路径：费用 4、0 目标、打出后作为 3 战力单位进入控制者基地。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json` 明确记录横置、摧毁友方对象、迅捷资源获得和反应限制路径暂缓。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 目前只覆盖普通手牌打出和带目标打出拒绝。
- `P4ActivatedAbilityCatalog` 当前只开放 Vi 与 Xerath 的代表性 `ACTIVATE_ABILITY`；没有 Malzahar resource-skill definition，也没有通用 destroy-friendly-unit-or-equipment-as-cost / gain temporary power-resource surface。
- `CoreRuleEngine.ResolveActivateAbility` 当前只有 battlefield experience ability、Xerath 分支和 Vi-like no-target paid skill 分支；Malzahar 命令会落入 unsupported command 或不匹配现有能力。

## 阻断原因

Malzahar 不能按 4C-86 / 4C-87 那样 evidence-only 入账，原因如下：

- official skill 是 activated resource skill，不是已有 direct `PLAY_CARD` resolve route。
- 成本同时包含横置来源和摧毁一个友方单位或装备；现有 activated ability catalog 没有该成本形状。
- 目标/成本对象是友方单位或装备，且应拒绝来源自身、敌方对象、手牌/牌堆/废牌堆对象、未知对象、face-down hidden 对手信息泄漏等边界。
- 资源结果是 `A A`，但只能用于支付符能费用；不能简单等同于永久 runePool generic power，除非同时记录限制语义和清理/消费边界。
- 迅捷/法术对决使用窗口与“获得费用资源的技能无法成为其他法术的反应目标”需要专门裁定：是立即结算的资源能力、还是 stack item 但不可被特定反应选中，不能偷用 Vi / Xerath 普通 stack ability 模型。
- FAQ refs 已标 `JFAQ-251023 p4` / `p5`，需要 E/D 做裁定摘录与冲突记录后再授权 B 写 runtime。

## 建议派单

B 服务端任务范围：

- 设计并实现最小 Malzahar activated resource skill surface，推荐不要复用 Vi 的 no-target stack 分支。
- 在 `ActionPrompt` 的 `ACTIVATE_ABILITY` metadata 中只向控制者暴露当前合法的 Malzahar source 与友方单位/装备成本对象候选。
- Core 命令侧拒绝 unsupported ability id、错误 source cardNo、非控制者 source、source 非场上正面单位、source 已横置、缺少/重复/非法 destroy-cost target、目标为 source 自身、敌方对象、非单位/非装备对象、隐藏/未知对象、错误时点。
- 成功路径应至少记录：source 横置、destroy-cost object 进入 owner graveyard、获得临时符能资源 `2`，并有明确事件 payload 区分 resource gain is payment-only。
- 若第一切片暂不实现 spell-duel / swift window，应显式不开放该窗口，并在文档保留 P1；不要在 prompt 中声称已支持迅捷 / 法术对决。

D/E 证据任务范围：

- 对 `JFAQ-251023 p4` / `p5` 中资源能力、法术对决、反应目标限制相关条目做裁定摘要。
- 更新 rule evidence index 时必须说明该技能是否入栈、是否可被反应、获得资源的使用/清理时点，以及本切片未覆盖项。
- 若 B 只实现己方开放主阶段代表切片，E 不能把 `FU-0f7cbe26ce` 升级为 full-official。

C 前端任务范围：

- 仅在服务端 `ActionPrompt` 暴露 Malzahar ability source / cost choices 后展示并提交。
- 不在前端自行推断友方单位/装备、资源可用性、迅捷窗口或反应限制。
- 需要最小 UI smoke 时，只验证服务端候选显示、选择成本对象、提交命令、authoritative snapshot 更新和 reload/reconnect。

## 最小验收建议

- focused backend：`Malzahar|ActivateAbility|DestroyFriendly|ResourceSkill`
- adjacent backend：`ActivateAbility|DestroyFriendly|RunePool|Payment|ActionPrompt|SpellDuel|Priority`
- 后端 full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
- 若触及 frontend prompt UI：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 与 Chrome smoke / targeted UI smoke。
- `git diff --check` 必须通过。

## 明确非覆盖

本 design gate 不关闭 `FU-0f7cbe26ce`；不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`；不授予 `stage4C88` representative evidence；不关闭 complete swift / spell-duel resource ability timing、complete PaymentEngine、resource restriction lifecycle、reaction target prohibition、hidden-info / redaction matrix、FAQ full adjudication、1009/811 full-official 或 READY。
