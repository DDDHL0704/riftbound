# Stage 4D-03J PaymentEngine Resource Skill Lifecycle Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03J 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码边界、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03I 已把 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill 接入 open-main representative path。4D-03J 的目标是继续收窄同一 official skill 的生命周期缺口，优先处理：

- spell-duel / swift timing：在法术对决焦点窗口允许当前焦点玩家使用该 resource skill。
- reaction prohibition：该 resource skill 不创建普通可反应 stack item，不能成为 counter / reaction spell 的目标。
- payment-only lifecycle：生成的 2 点通用符能必须有可审计的 payment-only lifecycle，不能长期混入无约束 `RunePool.Power` 后被任意费用消费。

候选仍为：

- `OGN·113/298`，卡名 `玛尔扎哈` / `Malzahar`，cardId `31332`，FU `FU-0f7cbe26ce`。
- 官方文本：摧毁一个友方单位或装备，横置；迅捷获得 `A A`，用以支付符能费用；可在己方回合或法术对决中使用；获得费用资源的技能无法成为其他法术的反应目标。
- 设计门禁：`docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`。
- 4D-03I 审计与证据：`docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.MalzaharResourceAbilityId` 已登记，并明确备注当前只开放 open-main representative；swift、spell-duel、reaction prohibition 与完整 payment-only lifecycle deferred。
- `CoreRuleEngine.ResolveMalzaharResourceSkill` 当前要求 `MAIN / NEUTRAL_OPEN`、active player 等于 intent player、stack 为空；`SPELL_DUEL_OPEN` 会被 `PHASE_NOT_ALLOWED` 拒绝且 no-mutation。
- `MatchSession.BuildPrompts` 在 `HasOpenSpellDuelFocus` 分支只公开 `PLAY_CARD`、`LEGEND_ACT` 与 `PASS_FOCUS`；不会公开 `ACTIVATE_ABILITY`。
- `ActionPromptBuilder.SpellDuelFocusActions` 当前没有 `ACTIVATE_ABILITY`。
- 4D-03I 成功路径将 2 点 payment-only power 加入 `RunePool.Power`，并仅通过 event / prompt metadata 标记 `PAY_RUNE_COSTS_ONLY_REPRESENTATIVE_4D_03I`；尚未有独立 temporary resource pool、pending payment binding 或清理/消费 lifecycle。
- 当前 tests 已有 `MalzaharPromptDoesNotOpenSpellDuelRepresentativeWindow` 与 `spell-duel` invalid source no-mutation guard；4D-03J 实现时应替换这些旧期望，而不是保留“spell duel 不开放”的断言。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`，仅当需要正式表达 payment-only resource lifecycle / quote / commit 时使用
- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 必要时补充窄域 spell-duel / reaction / payment-only lifecycle 测试文件

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- 与 Malzahar lifecycle 无关的 card behavior registry / fixture mass update
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`

## 4. 实现要求

### 4.1 Spell-Duel Prompt / Timing

- 在 `SPELL_DUEL_OPEN` 且 `FocusPlayerId == playerId` 时，`ActionPrompt` 可以公开 `ACTIVATE_ABILITY`。
- 只公开当前焦点玩家控制的 Malzahar source，且 source 必须公开、正面、未横置、位于服务端可验证区域。
- 成本目标仍必须是公开友方单位或装备，排除 source 自身、敌方、隐藏、手牌、非单位/非装备和未知对象。
- 非焦点玩家、closed timing、普通 stack priority、cleanup/task queue blocking 期间不得公开该 resource skill。
- Spell-duel 成功结算后应保持 `TimingState=SPELL_DUEL_OPEN`，焦点仍由当前 spell duel 状态机管理，不绕过 `PASS_FOCUS` / cleanup / battle promotion。

### 4.2 Command Guard

- `ResolveMalzaharResourceSkill` 应接受两类 timing：
  - open-main representative：沿用 4D-03I 行为。
  - spell-duel focus representative：仅当前玩家是 `FocusPlayerId` 时接受。
- 错误 timing 必须 rejected no-mutation：tick、zones、cardObjects、runePool、stack、focus/priority、pending task queue 均不变。
- 若 spell-duel focus accepted，不得创建普通 `StackItemState`；事件 payload 应明确 `timingContext=SPELL_DUEL_OPEN`、`resourceSkill=true`、`paymentOnly=true`。

### 4.3 Reaction Prohibition

- Malzahar resource skill 不应作为普通 spell / ability stack target 暴露给 counter / reaction spells。
- 如果实现继续采用立即结算，必须补测试证明：
  - 成功后没有 `STACK_ITEM_ADDED`。
  - 反应方没有新的 stack priority window。
  - counter / reaction target choices 不包含 Malzahar resource skill 的 audit-only pseudo item。
- 如果 B 选择建立专门 resource-skill stack object，则必须同时实现不可被 reaction spell 选择的 target filter；不要引入普通可反应 stack item。

### 4.4 Payment-Only Lifecycle

- 不能继续只依赖 `RunePool.Power` 加 2 并靠 payload 注释表达完整语义。
- 建议最小实现方式：
  - 建立 temporary payment resource ledger，记录 owner、sourceObjectId、abilityId、paymentWindow、generatedPower、remainingPower、allowedPaymentKinds。
  - prompt / snapshot 可审计地展示该资源只可用于支付符能费用。
  - `PAY_COST` / payment commit 只在 rune-cost payment window 中可消费该资源。
  - 不允许该资源支付 mana、experience、non-rune costs 或 unrelated score costs。
  - 同一支付步骤结束后未使用资源应被清理，并写入 audit event 或 state marker。
- 若本切片只完成 spell-duel timing，而 payment-only lifecycle 仍保留代表性 metadata，必须在审计中显式声明残余，不能关闭 P0-005。

## 5. 必补测试

Focused tests 至少覆盖：

- open-main 4D-03I 成功路径仍通过。
- spell-duel focus prompt 暴露 Malzahar `ACTIVATE_ABILITY` 与合法成本目标。
- 非焦点玩家 spell-duel prompt 不暴露 Malzahar resource skill。
- spell-duel focus 成功路径横置 source、摧毁成本对象、获得 payment-only resource，保持 spell duel open 且不创建 stack item。
- spell-duel wrong focus、closed timing、stack priority、cleanup blocking 均 rejected no-mutation。
- 成功后 reaction/counter prompt 不可选择 Malzahar resource skill。
- payment-only resource 不可支付非 rune-cost window；若可被后续 `PAY_COST` 消费，必须断言消费后 remaining / cleanup lifecycle。

相邻回归必须覆盖：

- 既有 Vi / Xerath `ACTIVATE_ABILITY`。
- 4D-03D payment resource action。
- 4D-03H trigger payment resource action。
- spell-duel focus / stack resolution。
- priority / reaction counter 代表路径。
- ActionPrompt / GameHub seed prompt surface。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~SpellDuel|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

实现通过后由 A 决定是否再跑 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要把 4D-03J 写成 full `[A]` / `[C]` resource skill family。
- 不要把 payment-only 资源当作无约束永久 `RunePool.Power` 后宣称 full official。
- 不要在 spell-duel focus 以外的反应 / priority / cleanup 窗口公开 Malzahar resource skill。
- 不要创建普通可被 counter / reaction spell 选中的 stack item。
- 不要修改 coverage matrix 或升级 `FU-0f7cbe26ce` 到 full-official。
- 不要改前端以本地推断补服务端 prompt 缺口。
- 不要因为 4D-03J 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03J 是 4D-03I 的后续生命周期切片，只收窄 Malzahar resource skill 的 spell-duel / reaction / payment-only lifecycle 风险。它可以推进 P0-005 PaymentEngine breadth，但不能替代完整 `[A]` / `[C]` family、LEGEND_ACT resource action、所有 payment window quote parity、LayerEngine 或最终 READY audit。
