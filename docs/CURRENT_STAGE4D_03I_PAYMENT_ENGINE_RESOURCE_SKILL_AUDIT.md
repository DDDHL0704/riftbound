# Stage 4D-03I PaymentEngine Resource Skill Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 4D-03I 的 A 侧实现审计。该切片只验收 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill 的 open-main representative path：服务端 prompt 暴露来源和合法友方单位/装备成本对象，命令侧横置来源、摧毁成本对象、获得 2 点带 payment-only metadata 的通用符能，并保持立即结算、无普通 stack item。该切片不关闭 P0-005，不升级为完整 `[A]` / `[C]` resource skill family、swift / spell-duel timing、reaction prohibition 或完整 payment-only resource lifecycle。

## 1. Scope

实现范围：

- `P4ActivatedAbilityCatalog` 新增 Malzahar resource skill definition，记录 `resourceSkill`、`paymentOnly`、`generatedPower=2`、`usesTargetAsCost` 与 restriction metadata。
- `MatchSession` 的 `ACTIVATE_ABILITY.sourceRequirements` 对 Malzahar 来源暴露一号成本目标槽，只列出同控制者公开正面友方单位或装备，排除自身、隐藏对象、敌方对象、非单位/非装备对象与错误区域对象。
- `CoreRuleEngine.ResolveActivateAbility` 新增 Malzahar 分支，只开放主动玩家 `MAIN / NEUTRAL_OPEN` 且当前 stack 为空的代表路径。
- 成功路径横置 Malzahar、摧毁成本对象至 owner graveyard、获得 2 点 generic power，并在 `ABILITY_ACTIVATED`、`UNIT_EXHAUSTED`、`UNIT_DESTROYED` / `EQUIPMENT_DESTROYED`、`POWER_GAINED` payload 中写入 `paymentWindow=ACTIVATE_ABILITY`、`abilityId`、`sourceObjectId`、`destroyedCostObjectId`、`resourceSkill=true`、`paymentOnly=true`、`generatedPower=2` 与 restriction metadata。

非范围：

- 不开放 spell-duel / swift 使用窗口。
- 不创建普通可反应 stack item。
- 不实现“获得费用资源的技能无法成为其他法术反应目标”的完整规则模型。
- 不实现完整 `[A]` / `[C]` resource skill family。
- 不修改前端、卡牌覆盖矩阵或未跟踪的 `riftbound-dotnet.sln`。

## 2. Changed Files

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`

## 3. Acceptance

已验收：

- Prompt 只向控制者公开 Malzahar `ACTIVATE_ABILITY` source requirement。
- 友方公开正面单位与装备作为 destroy-cost target 可选；自身、敌方、隐藏、手牌、法术、未知对象均不可选或被命令侧拒绝。
- 成功命令横置来源、摧毁成本对象到 owner graveyard、更新 `ObjectLocations`、增加 2 点 power、保持 stack 为空。
- 事件 payload 明确 resource skill / payment-only / generatedPower / restriction metadata。
- 无效来源、无效成本目标、spell-duel / closed timing 均 rejected no-mutation。
- 既有 Vi / Xerath `ACTIVATE_ABILITY`、PaymentEngine resource action、ActionPrompt、GameHub、SpellDuel、Priority 相邻路径保持绿色。

## 4. Residual Risk

- 当前用 `RunePool.Power` 承载代表性 payment-only `A A`，并通过 payload / prompt restriction metadata 标记为 `PAY_RUNE_COSTS_ONLY_REPRESENTATIVE_4D_03I`；尚未实现独立 payment-only pool 或同一支付步骤生命周期约束。
- 官方文本允许己方回合或法术对决中使用；本切片只开放 open-main representative，不开放 spell-duel / swift。
- 官方文本要求获得费用资源的技能不能成为其他法术的反应目标；本切片通过立即结算且不创建普通 stack item 避免反应目标，但未实现完整 reaction prohibition engine。
- P0-005 full PaymentEngine breadth 仍未关闭，完整 `[A]` / `[C]`、全支付窗口、替代/额外/可选费用矩阵与 prompt quote parity 仍需后续切片。

## 5. Verdict

4D-03I focused slice accepted。它把 Malzahar resource skill 作为 P0-005 resource skill breadth 的第一个 concrete representative 接入服务端权威 prompt / command / audit 面，但项目整体仍 **NOT READY**。
